namespace RoleChooser.Services;

/// <summary>
/// Session-scoped service that tracks which roles are currently active.
/// "Default" role (configurable) is always active and not included here.
/// </summary>
public interface IActiveRoleFilter
{
    /// <summary>
    /// Get the IDs of roles the user has chosen to activate (excludes always-active role).
    /// </summary>
    IReadOnlySet<Guid> ActiveRoleIds { get; }

    /// <summary>
    /// All role IDs assigned to the user (loaded on login, excludes always-active role).
    /// </summary>
    IReadOnlyList<(Guid Id, string Name)> AvailableRoles { get; }

    /// <summary>
    /// The always-active role ID (e.g., "Default" role).
    /// </summary>
    Guid? AlwaysActiveRoleId { get; }

    /// <summary>
    /// Set the available roles for the current user. Called after login.
    /// </summary>
    void Initialize(Guid? alwaysActiveRoleId, IEnumerable<(Guid Id, string Name)> availableRoles);

    /// <summary>
    /// Update which roles are active. Does not affect the always-active role.
    /// </summary>
    void SetActiveRoles(IEnumerable<Guid> roleIds);

    /// <summary>
    /// Check if a specific role is currently active (or is the always-active role).
    /// </summary>
    bool IsRoleActive(Guid roleId);
}
