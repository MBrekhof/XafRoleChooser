using RoleChooser.Services;

namespace RoleChooser.Security;

public static class RoleFilterAccessor
{
    private static readonly AsyncLocal<IActiveRoleFilter?> _current = new();

    public static IActiveRoleFilter? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
