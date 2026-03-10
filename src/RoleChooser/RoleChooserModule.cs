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
        var userId = (Guid)app.Security.UserId;

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

                    if (string.Equals(roleName, AlwaysActiveRoleName, StringComparison.OrdinalIgnoreCase))
                    {
                        alwaysActiveRoleId = roleId;
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

        filter.Initialize(alwaysActiveRoleId, availableRoles);

        // NOW set the ambient accessor — subsequent Roles calls will be filtered
        RoleFilterAccessor.Current = filter;
    }

    public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
    {
        return ModuleUpdater.EmptyModuleUpdaters;
    }
}
