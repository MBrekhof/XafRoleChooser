using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace XafRoleChooser.Module.BusinessObjects
{
    public enum Department
    {
        Management,
        Engineering,
        Sales,
        Finance,
        HumanResources,
        Operations
    }

    [DefaultClassOptions]
    [DefaultProperty(nameof(FullName))]
    [NavigationItem("HR")]
    public class Employee : BaseObject
    {
        [Required]
        public virtual string FirstName { get; set; }

        [Required]
        public virtual string LastName { get; set; }

        [EmailAddress]
        public virtual string Email { get; set; }

        [Phone]
        public virtual string Phone { get; set; }

        public virtual string JobTitle { get; set; }

        public virtual Department Department { get; set; }

        public virtual DateTime HireDate { get; set; }

        public virtual decimal Salary { get; set; }

        public virtual Company Company { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
}
