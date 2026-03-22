using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.ComponentModel.DataAnnotations;

namespace XafRoleChooser.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem("Sales")]
    public class OrderLine : BaseObject
    {
        [Required]
        public virtual string ProductName { get; set; }

        public virtual int Quantity { get; set; }

        public virtual decimal UnitPrice { get; set; }

        public virtual Order Order { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
