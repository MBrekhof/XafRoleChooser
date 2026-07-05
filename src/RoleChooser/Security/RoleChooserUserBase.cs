using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoleChooser.Services;

namespace RoleChooser.Security;

public abstract class RoleChooserUserBase : PermissionPolicyUser
{
    private static ILogger? _logger;

    public static void SetLogger(ILoggerFactory? loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<RoleChooserUserBase>();
    }

    public override IList<PermissionPolicyRole> Roles
    {
        get
        {
            var allRoles = base.Roles;
            // Resolve the filter from THIS user's own object space (BaseObject.ObjectSpace, set by
            // the EFCoreObjectSpace that materialized it) → its *circuit-scoped* service provider.
            // That scope is per Blazor circuit / session, so two concurrent logins of the same
            // account resolve independent filters — no process-wide static keyed by user id (RC-006).
            // If the object space isn't available (untracked instance) or the session was never
            // narrowed, pass through the real collection so M2M role-assignment writes persist.
            var filter = ObjectSpace?.ServiceProvider?.GetService<IActiveRoleFilter>();
            if (filter == null || !filter.IsFiltering || allRoles is not { Count: > 0 })
            {
                _logger?.LogDebug("Roles getter — pass-through ({Reason}) [os#{Os}], returning {Count} unfiltered roles",
                    filter == null ? "no scoped filter" : "not filtering",
                    ObjectSpace?.GetHashCode(), allRoles?.Count ?? 0);
                return allRoles;
            }

            var filtered = allRoles.Where(r => filter.IsRoleActive(r.ID)).ToList();
            var filteredNames = string.Join(", ", filtered.Select(r => r.Name));
            _logger?.LogDebug("Roles getter — filtering {AllCount} -> {FilteredCount} roles [os#{Os}]: [{FilteredNames}]",
                allRoles.Count, filtered.Count, ObjectSpace?.GetHashCode(), filteredNames);
            return filtered;
        }
        set => base.Roles = value;
    }

    /// <summary>
    /// Access ALL assigned roles regardless of the active filter.
    /// Use this for role management and the role chooser UI.
    /// </summary>
    public IList<PermissionPolicyRole> GetAllRoles() => base.Roles;
}
