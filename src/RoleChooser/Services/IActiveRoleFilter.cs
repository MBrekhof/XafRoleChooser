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
    /// The always-active role name, cached during Initialize.
    /// </summary>
    string? AlwaysActiveRoleName { get; }

    /// <summary>
    /// Id of the logged-on user this filter belongs to. The Roles override applies the filter
    /// only to this user's own object — never to other users loaded in the same session (e.g.
    /// the Users ListView or another user's DetailView opened by an admin).
    /// </summary>
    Guid OwnerUserId { get; }

    /// <summary>
    /// True when this user may use the login-time chooser (i.e. is a member of the configured
    /// administrator role). Non-admins log in with all their roles active and are never prompted.
    /// </summary>
    bool ChooserEnabled { get; }

    /// <summary>
    /// Set the available roles for the current user. Called after login.
    /// The always-active role is passed separately (id + name) because it is intentionally
    /// excluded from <paramref name="availableRoles"/> — the chooser must not offer it.
    /// </summary>
    void Initialize(Guid ownerUserId, Guid? alwaysActiveRoleId, string? alwaysActiveRoleName, bool chooserEnabled, IEnumerable<(Guid Id, string Name)> availableRoles);

    /// <summary>
    /// Update which roles are active. Does not affect the always-active role.
    /// </summary>
    void SetActiveRoles(IEnumerable<Guid> roleIds);

    /// <summary>
    /// Check if a specific role is currently active (or is the always-active role).
    /// </summary>
    bool IsRoleActive(Guid roleId);

    /// <summary>
    /// True only when the user narrowed their roles (active set is a strict subset
    /// of available roles). When false, the Roles override must return the real
    /// tracked collection so M2M role assignment writes persist.
    /// </summary>
    bool IsFiltering { get; }

    /// <summary>
    /// True once the user has made (or skipped past) their session role selection.
    /// </summary>
    bool SelectionMade { get; }

    /// <summary>
    /// Raised after a session role selection has been fully applied (permissions reloaded and
    /// navigation recreated). WinForms consumers refresh their already-open views in place here,
    /// because re-executing the startup navigation would open a duplicate MDI document/tab.
    /// </summary>
    event EventHandler? SessionRolesApplied;

    /// <summary>
    /// Raises <see cref="SessionRolesApplied"/>. Called by the login-time chooser once a selection
    /// has been applied (permissions reloaded, navigation recreated).
    /// </summary>
    void NotifySessionRolesApplied();
}
