using Microsoft.Extensions.Logging;

namespace RoleChooser.Services;

public class ActiveRoleFilter : IActiveRoleFilter
{
    private readonly ILogger<ActiveRoleFilter> _logger;
    private HashSet<Guid> _activeRoleIds = new();
    private List<(Guid Id, string Name)> _availableRoles = new();

    public IReadOnlySet<Guid> ActiveRoleIds => _activeRoleIds;
    public IReadOnlyList<(Guid Id, string Name)> AvailableRoles => _availableRoles;
    public Guid? AlwaysActiveRoleId { get; private set; }

    public ActiveRoleFilter(ILogger<ActiveRoleFilter> logger)
    {
        _logger = logger;
    }

    public void Initialize(Guid? alwaysActiveRoleId, IEnumerable<(Guid Id, string Name)> availableRoles)
    {
        AlwaysActiveRoleId = alwaysActiveRoleId;
        _availableRoles = availableRoles.ToList();
        // Activate all roles by default — user can deactivate via the chooser
        _activeRoleIds = new HashSet<Guid>(_availableRoles.Select(r => r.Id));

        var roleNames = string.Join(", ", _availableRoles.Select(r => r.Name));
        _logger.LogInformation("Initialize — AlwaysActiveId: {AlwaysActiveId}, Count: {Count}, Roles: [{RoleNames}]",
            alwaysActiveRoleId, _availableRoles.Count, roleNames);
    }

    public void SetActiveRoles(IEnumerable<Guid> roleIds)
    {
        _activeRoleIds = new HashSet<Guid>(roleIds);

        var activeNames = _availableRoles
            .Where(r => _activeRoleIds.Contains(r.Id))
            .Select(r => r.Name);
        _logger.LogInformation("SetActiveRoles — Count: {Count}, Active: [{ActiveRoles}]",
            _activeRoleIds.Count, string.Join(", ", activeNames));
    }

    public bool IsRoleActive(Guid roleId)
    {
        if (AlwaysActiveRoleId.HasValue && roleId == AlwaysActiveRoleId.Value)
        {
            _logger.LogDebug("IsRoleActive — RoleId: {RoleId} is always-active, returning true", roleId);
            return true;
        }

        var result = _activeRoleIds.Contains(roleId);
        _logger.LogDebug("IsRoleActive — RoleId: {RoleId}, Result: {Result}", roleId, result);
        return result;
    }
}
