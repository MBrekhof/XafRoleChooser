using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoleChooser.BusinessObjects;
using RoleChooser.Security;
using RoleChooser.Services;

namespace RoleChooser.Controllers;

public class RoleChooserWindowController : WindowController
{
    private PopupWindowShowAction _chooseRolesAction;
    private IActiveRoleFilter? _roleFilter;
    private ILogger<RoleChooserWindowController>? _logger;

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
        _logger = Application.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserWindowController>();
    }

    private void ChooseRolesAction_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
    {
        if (_roleFilter == null) return;

        _logger?.LogInformation("CustomizePopupWindowParams — {Count} available roles", _roleFilter.AvailableRoles.Count);

        var os = Application.CreateObjectSpace(typeof(ActiveRoleSelection));
        var items = new BindingList<ActiveRoleSelection>();

        foreach (var (id, name) in _roleFilter.AvailableRoles)
        {
            var item = os.CreateObject<ActiveRoleSelection>();
            item.RoleId = id;
            item.RoleName = name;
            item.IsActive = _roleFilter.ActiveRoleIds.Contains(id);
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

        var listView = Application.CreateListView(os, typeof(ActiveRoleSelection), true);
        e.View = listView;
        e.DialogController.SaveOnAccept = false;
    }

    private void ChooseRolesAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
    {
        if (_roleFilter == null) return;

        var selectedItems = e.PopupWindowViewSelectedObjects.Cast<ActiveRoleSelection>().ToList();
        var selectedRoleIds = selectedItems.Select(i => i.RoleId).ToList();

        // Build before/after summary using ActiveRoleIds directly (avoids IsRoleActive log spam)
        var previousNames = _roleFilter.AvailableRoles
            .Where(r => _roleFilter.ActiveRoleIds.Contains(r.Id))
            .Select(r => r.Name).ToList();
        var selectedNames = selectedItems.Select(i => i.RoleName).ToList();

        _logger?.LogInformation(
            "Role switch — Active: [{ActiveRoles}] | Deactivated: [{Deactivated}] | Newly activated: [{Activated}] | Always-active: {AlwaysActive}",
            string.Join(", ", selectedNames),
            string.Join(", ", previousNames.Except(selectedNames)),
            string.Join(", ", selectedNames.Except(previousNames)),
            _roleFilter.AlwaysActiveRoleName ?? "(none)");

        // Cache references before CloseAllTabs — closing tabs can deactivate the controller
        var app = Application;
        var frame = Frame;
        var window = Window;
        var navController = frame?.GetController<ShowNavigationItemController>();

        _roleFilter.SetActiveRoles(selectedRoleIds);

        // Close all open tabs to dispose stale ObjectSpaces
        CloseAllTabs(app, window);

        // Reload permissions — new ObjectSpaces will use the updated role filter
        if (app?.Security is ISecurityStrategyBase securityStrategy)
        {
            securityStrategy.ReloadPermissions();
            _logger?.LogInformation("Execute — ReloadPermissions called");
        }

        // Recreate navigation items so the nav tree reflects the new permissions
        if (navController != null)
        {
            navController.RecreateNavigationItems();
            _logger?.LogInformation("Execute — Navigation items recreated");

            var startupItem = navController.GetStartupNavigationItem();
            if (startupItem != null)
            {
                navController.ShowNavigationItemAction.DoExecute(startupItem);
                _logger?.LogInformation("Execute — Navigated to startup item");
            }
        }
        else if (frame?.View != null)
        {
            frame.View.ObjectSpace.Refresh();
        }
    }

    /// <summary>
    /// Closes all open tabs/documents to prevent stale ObjectSpaces after role switch.
    /// Blazor: BlazorMdiShowViewStrategy.CloseAllWindows() (closes Views + disposes ObjectSpaces).
    /// WinForms: ShowViewStrategy.Inspectors — close child windows only.
    /// </summary>
    private void CloseAllTabs(XafApplication? app, Window? window)
    {
        var template = window?.Template;
        if (template == null) return;

        // Blazor: close each MdiChildWindow via BlazorWindow.Close() which calls
        // View.Close(false) — properly disposes ObjectSpaces and fires template events.
        // We access MainWindow.MdiChildWindows and call Close() on each synchronously.
        if (window is object blazorMainWindow)
        {
            var mdiChildProp = blazorMainWindow.GetType().GetProperty("MdiChildWindows");
            if (mdiChildProp?.GetValue(blazorMainWindow) is System.Collections.IList mdiChildren && mdiChildren.Count > 0)
            {
                var clone = mdiChildren.Cast<object>().ToList();
                foreach (var child in clone)
                {
                    child.GetType().GetMethod("Close", Type.EmptyTypes)?.Invoke(child, null);
                }
                _logger?.LogInformation("Execute — Closed {Count} MDI child windows (Blazor)", clone.Count);
                return;
            }
        }

        // WinForms: close inspector windows (child tabs) via ShowViewStrategy.Inspectors
        if (app != null)
        {
            var winStrategy = app.ShowViewStrategy;
            var inspectorsProp = winStrategy?.GetType().GetProperty("Inspectors");
            if (inspectorsProp?.GetValue(winStrategy) is System.Collections.IList inspectors)
            {
                var clone = inspectors.Cast<object>().ToList();
                foreach (var inspector in clone)
                {
                    inspector.GetType().GetMethod("Close", Type.EmptyTypes)?.Invoke(inspector, null);
                }
                _logger?.LogInformation("Execute — Closed {Count} inspector windows (WinForms)", clone.Count);
            }
        }
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();
        _roleFilter = null;
        _logger = null;
    }
}
