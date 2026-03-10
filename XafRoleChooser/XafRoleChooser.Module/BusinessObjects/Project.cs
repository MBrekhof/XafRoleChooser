using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace XafRoleChooser.Module.BusinessObjects
{
    public enum ProjectStatus
    {
        Planning,
        InProgress,
        OnHold,
        Completed,
        Cancelled
    }

    [DefaultClassOptions]
    [DefaultProperty(nameof(Name))]
    [NavigationItem("Projects")]
    public class Project : BaseObject
    {
        [Required]
        public virtual string Name { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "nvarchar(max)")]
        public virtual string Description { get; set; }

        public virtual ProjectStatus Status { get; set; }

        public virtual DateTime StartDate { get; set; }

        public virtual DateTime? EndDate { get; set; }

        public virtual decimal Budget { get; set; }

        public virtual Company Company { get; set; }

        public virtual Employee Manager { get; set; }

        public virtual IList<Order> Orders { get; set; } = new ObservableCollection<Order>();
    }
}
