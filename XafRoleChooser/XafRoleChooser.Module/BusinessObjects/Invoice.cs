using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XafRoleChooser.Module.BusinessObjects
{
    public enum InvoiceStatus
    {
        Draft,
        Sent,
        Paid,
        Overdue,
        Cancelled
    }

    [DefaultClassOptions]
    [DefaultProperty(nameof(InvoiceNumber))]
    [NavigationItem("Finance")]
    public class Invoice : BaseObject
    {
        [Required]
        public virtual string InvoiceNumber { get; set; }

        public virtual DateTime InvoiceDate { get; set; }

        public virtual DateTime DueDate { get; set; }

        public virtual InvoiceStatus Status { get; set; }

        public virtual decimal Amount { get; set; }

        public virtual decimal TaxAmount { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal TotalAmount => Amount + TaxAmount;

        [Column(TypeName = "nvarchar(max)")]
        public virtual string Notes { get; set; }

        public virtual Company Company { get; set; }

        public virtual Order Order { get; set; }
    }
}
