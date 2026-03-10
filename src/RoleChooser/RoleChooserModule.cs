using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Updating;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoleChooser.BusinessObjects;
using RoleChooser.Security;
using RoleChooser.Services;

namespace RoleChooser;

public sealed class RoleChooserModule : ModuleBase
{
    /// <summary>
    /// Name of the role that is always active and hidden from the chooser UI.
    /// Defaults to "Default".
    /// </summary>
    public string AlwaysActiveRoleName { get; set; } = "Default";

    public RoleChooserModule()
    {
        AdditionalExportedTypes.Add(typeof(ActiveRoleSelection));
    }

    public override void Setup(XafApplication application)
    {
        base.Setup(application);
        application.SetupComplete += Application_SetupComplete;
        application.LoggedOn += Application_LoggedOn;
    }

    private void Application_SetupComplete(object? sender, EventArgs e)
    {
        var app = (XafApplication)sender!;

        // Warn if PermissionsReloadMode is not NoCache
        if (app.Security is SecurityStrategy strategy)
        {
            if (strategy.PermissionsReloadMode != PermissionsReloadMode.NoCache)
            {
                var logger = app.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserModule>();
                logger?.LogWarning(
                    "RoleChooserModule requires PermissionsReloadMode.NoCache for live role switching. " +
                    "Current mode is {Mode}. Role changes may not take effect until re-login.",
                    strategy.PermissionsReloadMode);
            }
        }
    }

    private void Application_LoggedOn(object? sender, LogonEventArgs e)
    {
        var app = (XafApplication)sender!;
        var filter = app.ServiceProvider.GetRequiredService<IActiveRoleFilter>();

        // Set the ambient accessor so the Roles override can access the filter
        RoleFilterAccessor.Current = filter;

        // Initialize the filter with the current user's roles
        using var os = app.CreateObjectSpace(typeof(PermissionPolicyRole));
        var userId = app.Security.UserId;

        // Get the user with ALL roles (bypass our filter by reading through ObjectSpace)
        var user = os.GetObjectByKey<PermissionPolicyUser>(userId);
        if (user == null) return;

        Guid? alwaysActiveRoleId = null;
        var availableRoles = new List<(Guid Id, string Name)>();

        foreach (PermissionPolicyRole role in user.Roles)
        {
            if (string.Equals(role.Name, AlwaysActiveRoleName, StringComparison.OrdinalIgnoreCase))
            {
                alwaysActiveRoleId = role.ID;
            }
            else
            {
                availableRoles.Add((role.ID, role.Name));
            }
        }

        filter.Initialize(alwaysActiveRoleId, availableRoles);
    }

    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
    {
        return ModuleUpdater.EmptyModuleUpdaters;
    }
}
