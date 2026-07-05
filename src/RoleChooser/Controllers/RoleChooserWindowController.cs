using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.SystemModule;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoleChooser.BusinessObjects;
using RoleChooser.Services;

namespace RoleChooser.Controllers;

/// <summary>
/// Shows the role-selection popup once, right after login (before the user
/// starts working), when the user has two or more optional roles. Selection
/// is per-session; changing roles afterwards requires re-login.
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
            _roleFilter is { SelectionMade: false } && _roleFilter.AvailableRoles.Count >= 2);
    }

    private void Application_ViewShown(object? sender, ViewShownEventArgs e)
    {
        TryShowLoginTimeChooser();
    }

    private void TryShowLoginTimeChooser()
    {
        if (_popupShown) return;
        if (_roleFilter is not { SelectionMade: false } || _roleFilter.AvailableRoles.Count < 2)
        {
            _logger?.LogInformation("Login-time chooser skipped — SelectionMade: {SelectionMade}, optional roles: {Count}",
                _roleFilter?.SelectionMade, _roleFilter?.AvailableRoles.Count ?? 0);
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
        _roleFilter.SetActiveRoles(_roleFilter.AvailableRoles.Select(r => r.Id));
        UpdateActionActive();
        _logger?.LogInformation("Chooser cancelled — all {Count} roles confirmed active", _roleFilter.AvailableRoles.Count);
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

        // Capture refs before any view churn — closing/replacing views can deactivate this controller.
        var app = Application;
        var window = Window;
        var navController = Frame?.GetController<ShowNavigationItemController>();

        _roleFilter.SetActiveRoles(selectedRoleIds);
        UpdateActionActive();

        // Usually no tabs exist at login time — but a browser refresh rebuilds the circuit and
        // restores the pre-refresh view(s), which render during the brief all-roles window before
        // this selection is applied. Close them so nothing opened under the broader role set
        // survives into the narrowed session (e.g. an Orders tab lingering after choosing HR).
        CloseAllTabs(app, window);

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

    /// <summary>
    /// Closes all open tabs/documents so no view opened under a broader role set survives a
    /// login-time selection (matters on refresh, which restores pre-refresh views). Verified
    /// per-platform mechanism — see the tab-closing dead-end map for the six approaches that fail.
    /// Blazor: close each <c>MainWindow.MdiChildWindows</c> via the parameterless
    /// <c>BlazorWindow.Close()</c> (calls <c>View.Close(false)</c>, disposes ObjectSpaces).
    /// WinForms: close each <c>ShowViewStrategy.Inspectors</c> window via the parameterless Close.
    /// </summary>
    private void CloseAllTabs(XafApplication? app, Window? window)
    {
        if (window?.Template == null) return;

        // Blazor: close each MDI child window synchronously.
        var mdiChildProp = window.GetType().GetProperty("MdiChildWindows");
        if (mdiChildProp?.GetValue(window) is System.Collections.IList mdiChildren && mdiChildren.Count > 0)
        {
            var clone = mdiChildren.Cast<object>().ToList();
            foreach (var child in clone)
            {
                child.GetType().GetMethod("Close", Type.EmptyTypes)?.Invoke(child, null);
            }
            _logger?.LogInformation("Closed {Count} MDI child windows (Blazor) on selection", clone.Count);
            return;
        }

        // WinForms: close inspector (child) windows. Resolve Close via GetMethod(name, EmptyTypes)
        // to avoid AmbiguousMatchException from the overloaded Close.
        var inspectorsProp = app?.ShowViewStrategy?.GetType().GetProperty("Inspectors");
        if (inspectorsProp?.GetValue(app!.ShowViewStrategy) is System.Collections.IList inspectors && inspectors.Count > 0)
        {
            var clone = inspectors.Cast<object>().ToList();
            foreach (var inspector in clone)
            {
                inspector.GetType().GetMethod("Close", Type.EmptyTypes)?.Invoke(inspector, null);
            }
            _logger?.LogInformation("Closed {Count} inspector windows (WinForms) on selection", clone.Count);
        }
    }
}
