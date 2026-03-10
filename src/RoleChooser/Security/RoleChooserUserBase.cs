using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;

namespace RoleChooser.Security;

public abstract class RoleChooserUserBase : PermissionPolicyUser
{
    public override IList<PermissionPolicyRole> Roles
    {
        get
        {
            var allRoles = base.Roles;
            var filter = RoleFilterAccessor.Current;
            if (filter == null || allRoles is not { Count: > 0 })
                return allRoles;

            return allRoles.Where(r => filter.IsRoleActive(r.ID)).ToList();
        }
        set => base.Roles = value;
    }

    /// <summary>
    /// Access ALL assigned roles regardless of the active filter.
    /// Use this for role management and the role chooser UI.
    /// </summary>
    [System.ComponentModel.Browsable(false)]
    public IList<PermissionPolicyRole> AllRoles => base.Roles;
}
