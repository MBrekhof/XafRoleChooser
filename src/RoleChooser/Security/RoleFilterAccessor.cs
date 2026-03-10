using System.Collections.Concurrent;
using RoleChooser.Services;

namespace RoleChooser.Security;

public static class RoleFilterAccessor
{
    private static readonly ConcurrentDictionary<Guid, IActiveRoleFilter> _filters = new();

    /// <summary>
    /// Register a filter for a user session. Called during LoggedOn.
    /// </summary>
    public static void Set(Guid userId, IActiveRoleFilter filter)
    {
        _filters[userId] = filter;
    }

    /// <summary>
    /// Get the filter for a user. Called from RoleChooserUserBase.Roles.
    /// </summary>
    public static IActiveRoleFilter? Get(Guid userId)
    {
        _filters.TryGetValue(userId, out var filter);
        return filter;
    }

    /// <summary>
    /// Remove the filter for a user. Called on logout/session end.
    /// </summary>
    public static void Remove(Guid userId)
    {
        _filters.TryRemove(userId, out _);
    }

    // Keep backward compat for AsyncLocal pattern (used during initialization)
    private static readonly AsyncLocal<IActiveRoleFilter?> _current = new();

    public static IActiveRoleFilter? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
