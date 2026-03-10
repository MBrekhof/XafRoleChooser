using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.Extensions.DependencyInjection;
using RoleChooser.BusinessObjects;
using RoleChooser.Security;
using RoleChooser.Services;

namespace RoleChooser.Controllers;

public class RoleChooserWindowController : WindowController
{
    private PopupWindowShowAction _chooseRolesAction;
    private IActiveRoleFilter? _roleFilter;

    public RoleChooserWindowController()
    {
        TargetWindowType = WindowType.Main;

        _chooseRolesAction = new PopupWindowShowAction(this, "ChooseActiveRoles", DevExpress.Persistent.Base.PredefinedCategory.Tools.ToString())
        {
            Caption = "Active Roles",
            ImageName = "Security_Role",
            ToolTip = "Choose which roles are active for this session"
        };
        _chooseRolesAction.CustomizePopupWindowParams += ChooseRolesAction_CustomizePopupWindowParams;
        _chooseRolesAction.Execute += ChooseRolesAction_Execute;
    }

    protected override void OnActivated()
    {
        base.OnActivated();
        _roleFilter = Application.ServiceProvider.GetService<IActiveRoleFilter>();
    }

    private void ChooseRolesAction_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
    {
        if (_roleFilter == null) return;

        var os = Application.CreateObjectSpace(typeof(ActiveRoleSelection));
        var items = new BindingList<ActiveRoleSelection>();

        foreach (var (id, name) in _roleFilter.AvailableRoles)
        {
            var item = os.CreateObject<ActiveRoleSelection>();
            item.RoleId = id;
            item.RoleName = name;
            item.IsActive = _roleFilter.IsRoleActive(id);
            items.Add(item);
        }

        // NonPersistentObjectSpace needs ObjectsGetting to provide objects for ListView
        if (os is NonPersistentObjectSpace npOs)
        {
            npOs.ObjectsGetting += (s, args) =>
            {
                if (args.ObjectType == typeof(ActiveRoleSelection))
                {
                    args.Objects = items;
                }
            };
        }

        e.View = Application.CreateListView(os, typeof(ActiveRoleSelection), true);
        e.DialogController.SaveOnAccept = false;
    }

    private void ChooseRolesAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
    {
        if (_roleFilter == null) return;

        var selectedRoleIds = new List<Guid>();
        foreach (ActiveRoleSelection item in e.PopupWindowView.ObjectSpace.GetObjects<ActiveRoleSelection>())
        {
            if (item.IsActive)
            {
                selectedRoleIds.Add(item.RoleId);
            }
        }

        _roleFilter.SetActiveRoles(selectedRoleIds);

        // Reload permissions so changes take effect immediately
        if (Application.Security is ISecurityStrategyBase securityStrategy)
        {
            securityStrategy.ReloadPermissions();
        }

        // Refresh the current view to reflect new permissions
        if (Frame?.View != null)
        {
            Frame.View.ObjectSpace.Refresh();
        }
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();
        _roleFilter = null;
    }
}
