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
                    "RoleChooserModule requires PermissionsReloadMode.NoCache for live role switching. " +
                    "Current mode is {Mode}. Role changes may not take effect until re-login.",
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

        // Ensure filter accessor is null so the Roles override returns base.Roles (unfiltered)
        RoleFilterAccessor.Current = null;

        Guid? alwaysActiveRoleId = null;
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

        logger?.LogInformation("Loaded {TotalRoles} available roles (plus always-active: {AlwaysActiveId})",
            availableRoles.Count, alwaysActiveRoleId);

        filter.Initialize(alwaysActiveRoleId, availableRoles);

        // Register filter by user ID (survives Blazor Server async boundaries)
        RoleFilterAccessor.Set(userId, filter);
        // Also set AsyncLocal for same-thread calls during initialization
        RoleFilterAccessor.Current = filter;

        logger?.LogInformation("RoleFilterAccessor set for user {UserId} — filter initialized", userId);
    }

    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
    {
        return ModuleUpdater.EmptyModuleUpdaters;
    }
}
