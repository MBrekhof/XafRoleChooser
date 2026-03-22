using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace XafRoleChooser.Module.BusinessObjects
{
    public enum OrderStatus
    {
        Draft,
        Submitted,
        Approved,
        Shipped,
        Delivered,
        Cancelled
    }

    [DefaultClassOptions]
    [DefaultProperty(nameof(OrderNumber))]
    [NavigationItem("Sales")]
    public class Order : BaseObject
    {
        [Required]
        public virtual string OrderNumber { get; set; }

        public virtual DateTime OrderDate { get; set; }

        public virtual DateTime? ShipDate { get; set; }

        public virtual OrderStatus Status { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "nvarchar(max)")]
        public virtual string Notes { get; set; }

        public virtual Company Company { get; set; }

        public virtual Project Project { get; set; }

        public virtual IList<OrderLine> Lines { get; set; } = new ObservableCollection<OrderLine>();

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal Total => Lines?.Sum(l => l.LineTotal) ?? 0;
    }
}
