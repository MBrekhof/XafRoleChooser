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
            var filter = ObjectSpace?.ServiceProvider?.GetService<IActiveRoleFilter>();
            // Only narrow the LOGGED-IN user's OWN roles. A session's filter must never touch other
            // users loaded in that session (Users ListView rows, another user's DetailView opened by
            // an admin) — otherwise their roles would display, and worse SAVE, filtered by the admin's
            // own selection. Pass through the real collection when: no scoped filter, this is not the
            // session's own user, the session wasn't narrowed, or there are no roles to filter.
            if (filter == null || filter.OwnerUserId != this.ID || !filter.IsFiltering || allRoles is not { Count: > 0 })
            {
                _logger?.LogDebug("Roles getter — pass-through ({Reason}) [os#{Os}], returning {Count} unfiltered roles",
                    filter == null ? "no scoped filter" : filter.OwnerUserId != this.ID ? "other user" : "not filtering",
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
