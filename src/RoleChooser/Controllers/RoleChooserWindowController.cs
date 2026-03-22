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
            item.IsActive = _roleFilter.IsRoleActive(id);
            items.Add(item);
            _logger?.LogDebug("  Role: {RoleName} ({RoleId}) — IsActive: {IsActive}", name, id, item.IsActive);
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

        // Capture previous state for logging
        var previousRoles = _roleFilter.AvailableRoles
            .Where(r => _roleFilter.IsRoleActive(r.Id))
            .Select(r => r.Name).ToList();

        // Use row selection (checkboxes in the left column) to determine active roles
        var selectedItems = e.PopupWindowViewSelectedObjects.Cast<ActiveRoleSelection>().ToList();
        var selectedRoleIds = selectedItems.Select(i => i.RoleId).ToList();
        var selectedRoleNames = selectedItems.Select(i => i.RoleName).ToList();

        var deactivated = previousRoles.Except(selectedRoleNames).ToList();
        var activated = selectedRoleNames.Except(previousRoles).ToList();

        _logger?.LogInformation(
            "Role switch — Active: [{ActiveRoles}] | Deactivated: [{Deactivated}] | Newly activated: [{Activated}] | Always-active: {AlwaysActive}",
            string.Join(", ", selectedRoleNames),
            deactivated.Count > 0 ? string.Join(", ", deactivated) : "(none)",
            activated.Count > 0 ? string.Join(", ", activated) : "(none)",
            _roleFilter.AlwaysActiveRoleId.HasValue
                ? _roleFilter.AvailableRoles.FirstOrDefault(r => r.Id == _roleFilter.AlwaysActiveRoleId.Value).Name ?? "unknown"
                : "(none)");

        _roleFilter.SetActiveRoles(selectedRoleIds);

        // Reload permissions so changes take effect immediately
        if (Application.Security is ISecurityStrategyBase securityStrategy)
        {
            securityStrategy.ReloadPermissions();
            _logger?.LogInformation("Execute — ReloadPermissions called");
        }
        else
        {
            _logger?.LogWarning("Execute — No ISecurityStrategyBase found, permissions NOT reloaded");
        }

        // Close all open tabs to prevent access to views the user no longer has permission for
        CloseAllTabs();

        // Recreate navigation items so the nav tree reflects the new permissions
        var navController = Frame?.GetController<ShowNavigationItemController>();
        if (navController != null)
        {
            navController.RecreateNavigationItems();
            _logger?.LogInformation("Execute — Navigation items recreated");

            // Navigate to the startup item so the user lands on a permitted view
            var startupItem = navController.GetStartupNavigationItem();
            if (startupItem != null)
            {
                navController.ShowNavigationItemAction.DoExecute(startupItem);
                _logger?.LogInformation("Execute — Navigated to startup item");
            }
        }
        else
        {
            // Fallback: at least refresh the current view
            if (Frame?.View != null)
            {
                Frame.View.ObjectSpace.Refresh();
                _logger?.LogInformation("Execute — View refreshed ({ViewId})", Frame.View.Id);
            }
        }
    }

    /// <summary>
    /// Closes all open tabs in the tabbed MDI template.
    /// Uses reflection to stay platform-agnostic (no Blazor/WinForms dependency).
    /// </summary>
    private void CloseAllTabs()
    {
        var template = Window?.Template;
        if (template == null) return;

        var templateType = template.GetType();
        var childProp = templateType.GetProperty("ChildTemplates",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var closeMethod = templateType.GetMethod("CloseViewTemplate");

        if (childProp == null || closeMethod == null) return;

        var children = ((System.Collections.IEnumerable)childProp.GetValue(template)!)
            .Cast<object>().ToList();

        foreach (var child in children)
        {
            closeMethod.Invoke(template, new[] { child });
        }

        _logger?.LogInformation("Execute — Closed {Count} open tabs", children.Count);
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();
        _roleFilter = null;
        _logger = null;
    }
}
