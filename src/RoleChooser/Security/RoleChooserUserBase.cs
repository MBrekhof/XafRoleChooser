using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.Extensions.Logging;

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
            var filter = RoleFilterAccessor.Current;
            if (filter == null || allRoles is not { Count: > 0 })
            {
                _logger?.LogDebug("Roles getter — no filter or empty roles, returning {Count} unfiltered roles",
                    allRoles?.Count ?? 0);
                return allRoles;
            }

            var filtered = allRoles.Where(r => filter.IsRoleActive(r.ID)).ToList();
            var filteredNames = string.Join(", ", filtered.Select(r => r.Name));
            _logger?.LogDebug("Roles getter — filtering {AllCount} -> {FilteredCount} roles: [{FilteredNames}]",
                allRoles.Count, filtered.Count, filteredNames);
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
