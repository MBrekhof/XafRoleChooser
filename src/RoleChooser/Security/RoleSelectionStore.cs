using System.Collections.Concurrent;

namespace RoleChooser.Security;

/// <summary>
/// Process-wide, per-user store of the last active-role selection an admin made.
///
/// Deliberately keyed by user id (NOT per session): the selection is "sticky" so a browser
/// refresh — which tears down the Blazor circuit and re-runs login — restores it silently
/// instead of re-prompting with the chooser. It is cleared on an explicit logout, so the
/// chooser returns on the next login (see RoleChooserModule.LoggingOff). This is the trade-off
/// chosen for RC-006: concurrent sessions of one account share one selection rather than
/// holding independent ones.
///
/// ponytail: entries linger until logout — a user who just closes the browser leaves one
/// behind. Fine for a reusable demo module; swap for a size-bounded/expiring cache if a
/// long-running host ever accumulates enough stale users to matter.
/// </summary>
public static class RoleSelectionStore
{
    private static readonly ConcurrentDictionary<Guid, IReadOnlyCollection<Guid>> _selections = new();

    public static bool TryGet(Guid userId, out IReadOnlyCollection<Guid> roleIds)
        => _selections.TryGetValue(userId, out roleIds!);

    public static void Set(Guid userId, IReadOnlyCollection<Guid> roleIds)
        => _selections[userId] = roleIds;

    public static void Clear(Guid userId)
        => _selections.TryRemove(userId, out _);
}
