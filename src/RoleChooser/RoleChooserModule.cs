using System.Data;
using System.Data.Common;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.EFCore;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.Updating;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Only members of this role get the login-time chooser. Everyone else logs in with all
    /// their roles active and is never prompted. Defaults to "Administrators".
    /// </summary>
    public string AdministratorRoleName { get; set; } = "Administrators";

    public RoleChooserModule()
    {
        AdditionalExportedTypes.Add(typeof(ActiveRoleSelection));
    }

    public override void Setup(XafApplication application)
    {
        base.Setup(application);
        application.SetupComplete += Application_SetupComplete;
        application.LoggedOn += Application_LoggedOn;
        // LoggingOff (not LoggedOff) so the user id is still available — Security.Logoff() has
        // not run yet. Fires only on an explicit logout, not on a refresh/circuit teardown
        // (BlazorApplication.DisposeCore never calls LogOff), which is exactly what makes the
        // sticky selection survive refreshes but reset after a real logout.
        application.LoggingOff += Application_LoggingOff;
    }

    private void Application_SetupComplete(object? sender, EventArgs e)
    {
        var app = (XafApplication)sender!;
        var loggerFactory = app.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<RoleChooserModule>();

        // Set static logger on RoleChooserUserBase for the Roles override
        RoleChooserUserBase.SetLogger(loggerFactory);

        if (app.Security is SecurityStrategy strategy)
        {
            logger?.LogInformation("RoleChooserModule SetupComplete — PermissionsReloadMode: {Mode}", strategy.PermissionsReloadMode);

            if (strategy.PermissionsReloadMode != PermissionsReloadMode.NoCache)
            {
                logger?.LogWarning(
                    "RoleChooserModule requires PermissionsReloadMode.NoCache — without it the " +
                    "login-time role selection does not take effect (permissions stay cached from logon). " +
                    "Current mode is {Mode}.",
                    strategy.PermissionsReloadMode);
            }
        }
        else
        {
            logger?.LogInformation("RoleChooserModule SetupComplete — no SecurityStrategy found");
        }
    }

    private void Application_LoggedOn(object? sender, LogonEventArgs e)
    {
        var app = (XafApplication)sender!;
        var logger = app.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserModule>();
        var filter = app.ServiceProvider.GetRequiredService<IActiveRoleFilter>();
        var userId = (Guid)app.Security.UserId;

        logger?.LogInformation("Application_LoggedOn — UserId: {UserId}", userId);

        Guid? alwaysActiveRoleId = null;
        string? alwaysActiveRoleName = null;
        var availableRoles = new List<(Guid Id, string Name)>();

        // Use raw SQL to load roles directly from the join table.
        // This bypasses the RoleChooserUserBase.Roles override entirely,
        // avoiding issues with EF Core change tracking proxies and Include.
        using var os = app.CreateObjectSpace(typeof(PermissionPolicyUser));
        if (os is EFCoreObjectSpace efOs)
        {
            var db = efOs.DbContext.Database;
            var conn = db.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) conn.Open();

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT r.ID, r.Name " +
                    "FROM PermissionPolicyRoleBase r " +
                    "INNER JOIN PermissionPolicyRolePermissionPolicyUser ur ON r.ID = ur.RolesID " +
                    "WHERE ur.UsersID = @userId " +
                    "ORDER BY r.Name";

                var param = cmd.CreateParameter();
                param.ParameterName = "@userId";
                param.Value = userId;
                param.DbType = DbType.Guid;
                cmd.Parameters.Add(param);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var roleId = reader.GetGuid(0);
                    var roleName = reader.GetString(1);

                    logger?.LogDebug("Loaded role from SQL — Id: {RoleId}, Name: {RoleName}", roleId, roleName);

                    if (string.Equals(roleName, AlwaysActiveRoleName, StringComparison.OrdinalIgnoreCase))
                    {
                        alwaysActiveRoleId = roleId;
                        alwaysActiveRoleName = roleName;
                        logger?.LogDebug("Role '{RoleName}' ({RoleId}) is the always-active role", roleName, roleId);
                    }
                    else
                    {
                        availableRoles.Add((roleId, roleName));
                    }
                }
            }
            finally
            {
                if (!wasOpen) conn.Close();
            }
        }

        // The chooser is admin-only: a user gets it only if they're a member of the configured
        // administrator role. Everyone else keeps all their roles active and is never prompted.
        var chooserEnabled = availableRoles.Any(r =>
            string.Equals(r.Name, AdministratorRoleName, StringComparison.OrdinalIgnoreCase));

        logger?.LogInformation("Loaded {TotalRoles} available roles (plus always-active: {AlwaysActiveId}); chooser enabled: {ChooserEnabled}",
            availableRoles.Count, alwaysActiveRoleId, chooserEnabled);

        // The Roles override resolves this SAME scoped instance via the user object's
        // ObjectSpace.ServiceProvider (see RoleChooserUserBase).
        filter.Initialize(userId, alwaysActiveRoleId, alwaysActiveRoleName, chooserEnabled, availableRoles);

        // Sticky selection: if this admin already chose in a previous circuit (and hasn't logged
        // out), re-apply it silently so a browser refresh doesn't re-prompt. RoleSelectionStore
        // is keyed by user id and survives circuit teardown.
        if (chooserEnabled && RoleSelectionStore.TryGet(userId, out var stickyRoleIds))
        {
            filter.SetActiveRoles(stickyRoleIds);
            logger?.LogInformation("Applied sticky selection for user {UserId} — {Count} roles active, chooser suppressed",
                userId, stickyRoleIds.Count);
        }
        else
        {
            logger?.LogInformation("Scoped role filter initialized for user {UserId}", userId);
        }
    }

    private void Application_LoggingOff(object? sender, LoggingOffEventArgs e)
    {
        var app = (XafApplication)sender!;
        // Runs before Security.Logoff(), so UserId is still valid. Drop the sticky selection so
        // the next login prompts the chooser again. (If the logout is cancelled the entry is gone
        // and the admin re-picks on their next refresh — a rare, harmless nuisance.)
        if (app.Security?.UserId is Guid userId)
        {
            RoleSelectionStore.Clear(userId);
            app.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserModule>()
                .LogInformation("Cleared sticky role selection for user {UserId} on logout", userId);
        }
    }

    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
    {
        return ModuleUpdater.EmptyModuleUpdaters;
    }
}
