namespace RoleChooser.Services;

public class ActiveRoleFilter : IActiveRoleFilter
{
    private HashSet<Guid> _activeRoleIds = new();
    private List<(Guid Id, string Name)> _availableRoles = new();

    public IReadOnlySet<Guid> ActiveRoleIds => _activeRoleIds;
    public IReadOnlyList<(Guid Id, string Name)> AvailableRoles => _availableRoles;
    public Guid? AlwaysActiveRoleId { get; private set; }

    public void Initialize(Guid? alwaysActiveRoleId, IEnumerable<(Guid Id, string Name)> availableRoles)
    {
        AlwaysActiveRoleId = alwaysActiveRoleId;
        _availableRoles = availableRoles.ToList();
        _activeRoleIds.Clear();
    }

    public void SetActiveRoles(IEnumerable<Guid> roleIds)
    {
        _activeRoleIds = new HashSet<Guid>(roleIds);
    }

    public bool IsRoleActive(Guid roleId)
    {
        if (AlwaysActiveRoleId.HasValue && roleId == AlwaysActiveRoleId.Value)
            return true;
        return _activeRoleIds.Contains(roleId);
    }
}
