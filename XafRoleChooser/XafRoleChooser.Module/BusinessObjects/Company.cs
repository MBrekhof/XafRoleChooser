using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace XafRoleChooser.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(Name))]
    [NavigationItem("Company")]
    public class Company : BaseObject
    {
        [Required]
        public virtual string Name { get; set; }

        public virtual string Address { get; set; }

        public virtual string City { get; set; }

        public virtual string Country { get; set; }

        [Phone]
        public virtual string Phone { get; set; }

        [EmailAddress]
        public virtual string Email { get; set; }

        public virtual string TaxId { get; set; }

        public virtual IList<Employee> Employees { get; set; } = new ObservableCollection<Employee>();

        public virtual IList<Project> Projects { get; set; } = new ObservableCollection<Project>();

        public virtual IList<Order> Orders { get; set; } = new ObservableCollection<Order>();

        public virtual IList<Invoice> Invoices { get; set; } = new ObservableCollection<Invoice>();
    }
}
