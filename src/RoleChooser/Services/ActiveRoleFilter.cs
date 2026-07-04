using Microsoft.Extensions.Logging;

namespace RoleChooser.Services;

public class ActiveRoleFilter : IActiveRoleFilter
{
    private readonly ILogger<ActiveRoleFilter>? _logger;
    private HashSet<Guid> _activeRoleIds = new();
    private List<(Guid Id, string Name)> _availableRoles = new();

    public IReadOnlySet<Guid> ActiveRoleIds => _activeRoleIds;
    public IReadOnlyList<(Guid Id, string Name)> AvailableRoles => _availableRoles;
    public Guid? AlwaysActiveRoleId { get; private set; }
    public string? AlwaysActiveRoleName { get; private set; }
    // "Filtering" = the active set doesn't cover every available role.
    // Membership check, not a count proxy: an active set that swapped an
    // available role for an out-of-set id would have equal count but must
    // still count as filtering.
    public bool IsFiltering => _availableRoles.Any(r => !_activeRoleIds.Contains(r.Id));
    public bool SelectionMade { get; private set; }

    public ActiveRoleFilter(ILogger<ActiveRoleFilter>? logger = null)
    {
        _logger = logger;
    }

    public void Initialize(Guid? alwaysActiveRoleId, IEnumerable<(Guid Id, string Name)> availableRoles)
    {
        SelectionMade = false;
        AlwaysActiveRoleId = alwaysActiveRoleId;
        _availableRoles = availableRoles.ToList();
        _activeRoleIds = new HashSet<Guid>(_availableRoles.Select(r => r.Id));

        AlwaysActiveRoleName = alwaysActiveRoleId.HasValue
            ? _availableRoles.FirstOrDefault(r => r.Id == alwaysActiveRoleId.Value).Name
            : null;

        _logger?.LogInformation("Initialize — AlwaysActiveId: {AlwaysActiveId} ({AlwaysActiveName}), Count: {Count}, Roles: [{RoleNames}]",
            alwaysActiveRoleId, AlwaysActiveRoleName ?? "(none)", _availableRoles.Count,
            string.Join(", ", _availableRoles.Select(r => r.Name)));
    }

    public void SetActiveRoles(IEnumerable<Guid> roleIds)
    {
        _activeRoleIds = new HashSet<Guid>(roleIds);
        SelectionMade = true;

        var activeNames = _availableRoles
            .Where(r => _activeRoleIds.Contains(r.Id))
            .Select(r => r.Name);
        _logger?.LogInformation("SetActiveRoles — Count: {Count}, Active: [{ActiveRoles}]",
            _activeRoleIds.Count, string.Join(", ", activeNames));
    }

    public bool IsRoleActive(Guid roleId)
    {
        if (AlwaysActiveRoleId.HasValue && roleId == AlwaysActiveRoleId.Value)
            return true;

        return _activeRoleIds.Contains(roleId);
    }
}
