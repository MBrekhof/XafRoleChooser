using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.SystemModule;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoleChooser.BusinessObjects;
using RoleChooser.Security;
using RoleChooser.Services;

namespace RoleChooser.Controllers;

/// <summary>
/// Shows the role-selection popup once, right after login, when the user is a member of the
/// administrator role and has two or more optional roles. The selection is persisted per user
/// (<see cref="RoleSelectionStore"/>) so a browser refresh restores it silently instead of
/// re-prompting; it is reset on an explicit logout. Changing roles otherwise requires re-login.
/// </summary>
public class RoleChooserWindowController : WindowController
{
    private const string LoginTimeOnlyKey = "RoleChooser.LoginTimeOnly";

    private PopupWindowShowAction _chooseRolesAction;
    private IActiveRoleFilter? _roleFilter;
    private ILogger<RoleChooserWindowController>? _logger;
    private bool _popupShown;

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
        UpdateActionActive();
        // Views never land on the Blazor MDI main window (BlazorMdiShowViewStrategy
        // sets them on MDI child windows), so Window.ViewChanged is the wrong signal.
        // Application.ViewShown fires for every frame that shows a view — including
        // the startup view right after login.
        if (!_popupShown)
        {
            Application.ViewShown += Application_ViewShown;
        }
    }

    protected override void OnDeactivated()
    {
        Application.ViewShown -= Application_ViewShown;
        _roleFilter = null;
        _logger = null;
        base.OnDeactivated();
    }

    private void UpdateActionActive()
    {
        _chooseRolesAction.Active.SetItemValue(LoginTimeOnlyKey,
            _roleFilter is { SelectionMade: false, ChooserEnabled: true } && _roleFilter.AvailableRoles.Count >= 2);
    }

    private void Application_ViewShown(object? sender, ViewShownEventArgs e)
    {
        TryShowLoginTimeChooser();
    }

    private void TryShowLoginTimeChooser()
    {
        if (_popupShown) return;
        if (_roleFilter is not { SelectionMade: false, ChooserEnabled: true } || _roleFilter.AvailableRoles.Count < 2)
        {
            _logger?.LogInformation("Login-time chooser skipped — SelectionMade: {SelectionMade}, ChooserEnabled: {ChooserEnabled}, optional roles: {Count}",
                _roleFilter?.SelectionMade, _roleFilter?.ChooserEnabled, _roleFilter?.AvailableRoles.Count ?? 0);
            MarkShownAndUnsubscribe();
            return;
        }

        MarkShownAndUnsubscribe();
        _logger?.LogInformation("Showing login-time role chooser — {Count} optional roles", _roleFilter.AvailableRoles.Count);
        ShowChooserPopup();
    }

    private void MarkShownAndUnsubscribe()
    {
        _popupShown = true;
        Application.ViewShown -= Application_ViewShown;
    }

    /// <summary>
    /// Shows the chooser popup platform-agnostically. Mirrors the generic core of
    /// DevExpress's Blazor PopupWindowShowActionBinding.ShowPopupWindow (verified
    /// against installed source, v25.2): GetPopupWindowParams() wires the
    /// DialogController's Accepting event to this action's Execute handler.
    /// </summary>
    private void ShowChooserPopup()
    {
        var args = _chooseRolesAction.GetPopupWindowParams();
        if (args.View == null) return;

        // Cancel = "keep all roles" — counts as the session selection.
        args.DialogController.Cancelling += (s, e) => ConfirmAllRoles();

        var svp = new ShowViewParameters(args.View)
        {
            TargetWindow = TargetWindow.NewModalWindow,
            CreateAllControllers = true
        };
        svp.Controllers.AddRange(args.DialogController.Controllers);
        svp.Controllers.Add(args.DialogController);
        Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, _chooseRolesAction));
    }

    private void ConfirmAllRoles()
    {
        if (_roleFilter == null) return;
        PersistSelection(Application, _roleFilter.AvailableRoles.Select(r => r.Id));
        UpdateActionActive();
        _logger?.LogInformation("Chooser cancelled — all {Count} roles confirmed active", _roleFilter.AvailableRoles.Count);
    }

    /// <summary>
    /// Applies the selection to the scoped filter AND records it in the per-user sticky store,
    /// so a later circuit (browser refresh) restores it silently. Keyed by the logged-on user id.
    /// </summary>
    private void PersistSelection(XafApplication? app, IEnumerable<Guid> roleIds)
    {
        var ids = roleIds.ToList();
        _roleFilter!.SetActiveRoles(ids);
        if (app?.Security?.UserId is Guid userId)
        {
            RoleSelectionStore.Set(userId, ids);
        }
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
            item.IsActive = _roleFilter.ActiveRoleIds.Contains(id);
            items.Add(item);
        }

        // NonPersistentObjectSpace needs ObjectsGetting to provide objects for ListView
        if (os is NonPersistentObjectSpace npOs)
        {
            npOs.ObjectsGetting += (s, argsGetting) =>
            {
                if (argsGetting.ObjectType == typeof(ActiveRoleSelection))
                {
                    argsGetting.Objects = items;
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

        _logger?.LogInformation("Session roles selected — Active: [{ActiveRoles}] | Always-active: {AlwaysActive}",
            string.Join(", ", selectedItems.Select(i => i.RoleName)),
            _roleFilter.AlwaysActiveRoleName ?? "(none)");

        // Capture refs before any view churn — replacing views can deactivate this controller.
        var app = Application;
        var navController = Frame?.GetController<ShowNavigationItemController>();

        PersistSelection(app, selectedRoleIds);
        UpdateActionActive();

        // No CloseAllTabs needed: the chooser only fires at first login (sticky suppresses it on
        // refresh), and at first login no tabs exist yet.
        if (app?.Security is ISecurityStrategyBase securityStrategy)
        {
            securityStrategy.ReloadPermissions();
        }

        if (navController != null)
        {
            navController.RecreateNavigationItems();
            var startupItem = navController.GetStartupNavigationItem();
            if (startupItem != null)
            {
                navController.ShowNavigationItemAction.DoExecute(startupItem);
            }
        }
    }
}
