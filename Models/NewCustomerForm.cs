using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Reflection;

namespace SalesMetrics.Models
{
    public class NewCustomerFormRequest
    {
        public int ID { get; set; }
        public DateTime SubmittedDate { get; set; } = DateTime.Now;
        public string? SubmittedBy { get; set; }
        public int? SubmittedByUserId { get; set; }


        public PropertyInfo Property { get; set; }
        public ManagementCompanyInfo ManagementCompany { get; set; }
        public BillingInfo Billing { get; set; }
        public IncomeInfo Income { get; set; }
        public List<ProductLineItem> Products { get; set; } = new();
        public string? SpecialNotes { get; set; }
        public string? BillingInstructions { get; set; }
        public string? LocationCode { get; set; }
    }

    public class PropertyInfo
    {
        public string Name { get; set; }
        public decimal CreditLine { get; set; }
        public string? ShipToName { get; set; }
        public string? ShipToAddress { get; set; }
        public Contact ReceivingContact { get; set; }
        public Contact PropertyContact { get; set; }
        public int? Units { get; set; }
    }

    public class ManagementCompanyInfo
    {
        public string ManagementName { get; set; }
        public string? ManagementAddress { get; set; }
        public Contact RegionalManager { get; set; }
        public Contact PropertyManager { get; set; }
    }

    public class BillingInfo
    {
        public Contact AccountsPayable { get; set; }
        public Contact AlternateAPContact { get; set; }
        public bool RequiresPDFInvoices { get; set; }
        public bool RequiresSpectrumInvoices { get; set; }

        public string? InvoiceEmail { get; set; }
        public string? InvoiceAddress { get; set; }
        public string? InvoiceCity { get; set; }
        public string? InvoiceState { get; set; }
        public string? InvoiceZip { get; set; }
        public string? InvoiceAttention { get; set; }

        public string? ThirdPartyVendor { get; set; }
        public bool RequiresCustomerPO { get; set; }
    }

    public class IncomeInfo
    {
        public string SalespersonName { get; set; }
        public decimal EstimatedMonthlyRevenue { get; set; }
        public decimal ARCreditLimit { get; set; }
        public string? Terms { get; set; }
        public string? DiscountPercent { get; set; }
    }

    public class Contact
    {
        public string? ContactType { get; set; } // e.g. "Receiving", "AP", "Regional Manager"
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }        
    }

    public class ProductLineItem
    {
        public int ID { get; set; }
        public string ProductClass { get; set; }
        public string Style { get; set; }
        public string Color { get; set; }
        public decimal Price { get; set; }
    }

    public class NewCustomerListItem
    {
        public int ID { get; set; }
        public string PropertyName { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string SalespersonName { get; set; }
        public string? LocationCode { get; set; }
    }

}
