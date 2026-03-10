using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using System.ComponentModel;

namespace RoleChooser.BusinessObjects;

[DomainComponent]
[DefaultProperty(nameof(RoleName))]
public class ActiveRoleSelection
{
    [DevExpress.ExpressApp.Data.Key]
    [Browsable(false)]
    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
