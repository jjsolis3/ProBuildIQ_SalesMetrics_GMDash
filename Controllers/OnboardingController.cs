using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Data;
using SalesMetrics.Models;

namespace SalesMetrics.Controllers
{
    public class OnboardingController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly SalesMetricsDbContext _context;

        public OnboardingController(IConfiguration configuration, SalesMetricsDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Submissions");
        }

        public IActionResult ViewCustomerRequest(int requestId)
        {
            var request = GetCustomerRequestById(requestId); // This already returns NewCustomerFormRequest
            return PartialView("ViewRequestPartial", request); // ✅ Pass the full model
        }

        public NewCustomerFormRequest GetCustomerRequestById(int id)
        {
            var model = new NewCustomerFormRequest();

            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Get main request
                var cmd = new SqlCommand(@"
                    SELECT 
                        SubmittedDate, SubmittedBy, SubmittedByUserId,
                        PropertyName, CreditLine, ShipToName, ShipToAddress, PropertyUnits,
                        MgmtCompanyName, MgmtCompanyAddress,
                        RequiresPDFInvoices, RequiresSpectrumInvoices, InvoiceEmail,
                        InvoiceAddress, InvoiceCity, InvoiceState, InvoiceZip, InvoiceAttention,
                        ThirdPartyVendor, RequiresCustomerPO,
                        SalespersonName, EstimatedMonthlyRevenue, ARCreditLimit,
                        Terms, DiscountPercent, SpecialNotes, BillingInstructions
                    FROM NewCustomerRequests
                    WHERE ID = @ID
                ", conn);

                cmd.Parameters.AddWithValue("@ID", id);
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    model.ID = id;
                    model.SubmittedDate = reader.GetDateTime(0);
                    model.SubmittedBy = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    model.SubmittedByUserId = reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2));

                    model.Property = new PropertyInfo
                    {
                        Name = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        CreditLine = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                        ShipToName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        ShipToAddress = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        Units = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                        ReceivingContact = new Contact(),
                        PropertyContact = new Contact()
                    };

                    model.ManagementCompany = new ManagementCompanyInfo
                    {
                        ManagementName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        ManagementAddress = reader.IsDBNull(9) ? "" : reader.GetString(9),
                        RegionalManager = new Contact(),
                        PropertyManager = new Contact()
                    };

                    model.Billing = new BillingInfo
                    {
                        RequiresPDFInvoices = reader.IsDBNull(10) ? false : reader.GetBoolean(10),
                        RequiresSpectrumInvoices = reader.IsDBNull(11) ? false : reader.GetBoolean(11),
                        InvoiceEmail = reader.IsDBNull(12) ? "" : reader.GetString(12),
                        InvoiceAddress = reader.IsDBNull(13) ? "" : reader.GetString(13),
                        InvoiceCity = reader.IsDBNull(14) ? "" : reader.GetString(14),
                        InvoiceState = reader.IsDBNull(15) ? "" : reader.GetString(15),
                        InvoiceZip = reader.IsDBNull(16) ? "" : reader.GetString(16),
                        InvoiceAttention = reader.IsDBNull(17) ? "" : reader.GetString(17),
                        ThirdPartyVendor = reader.IsDBNull(18) ? "" : reader.GetString(18),
                        RequiresCustomerPO = reader.IsDBNull(19) ? false : reader.GetBoolean(19),
                        AccountsPayable = new Contact(),
                        AlternateAPContact = new Contact()
                    };

                    model.Income = new IncomeInfo
                    {
                        SalespersonName = reader.IsDBNull(20) ? "" : reader.GetString(20),
                        EstimatedMonthlyRevenue = reader.IsDBNull(21) ? 0 : reader.GetDecimal(21),
                        ARCreditLimit = reader.IsDBNull(22) ? 0 : reader.GetDecimal(22),
                        Terms = reader.IsDBNull(23) ? "" : reader.GetString(23),
                        DiscountPercent = reader.IsDBNull(24) ? "" : reader.GetString(24)
                    };

                    model.SpecialNotes = reader.IsDBNull(25) ? "" : reader.GetString(25);
                    model.BillingInstructions = reader.IsDBNull(26) ? "" : reader.GetString(26);

                }
                reader.Close();

                // Get product lines
                var productsCmd = new SqlCommand(@"
                    SELECT ProductClass, Style, Color, Price
                    FROM NewCustomerProducts
                    WHERE NewCustomerRequestID = @ID
                ", conn);

                productsCmd.Parameters.AddWithValue("@ID", id);
                var prodReader = productsCmd.ExecuteReader();
                model.Products = new List<ProductLineItem>();
                while (prodReader.Read())
                {
                    model.Products.Add(new ProductLineItem
                    {
                        ProductClass = prodReader.GetString(0),
                        Style = prodReader.GetString(1),
                        Color = prodReader.GetString(2),
                        Price = prodReader.GetDecimal(3)
                    });
                }
                prodReader.Close();

                // Get contact records
                var contactCmd = new SqlCommand(@"
                    SELECT ContactType, Name, Phone, Fax, Email
                    FROM NewCustomerContacts
                    WHERE NewCustomerRequestID = @ID
                ", conn);
                contactCmd.Parameters.AddWithValue("@ID", id);

                var contactReader = contactCmd.ExecuteReader();
                while (contactReader.Read())
                {
                    var contact = new Contact
                    {
                        ContactType = contactReader.GetString(0),
                        Name = contactReader.GetString(1),
                        Phone = contactReader.GetString(2),
                        Fax = contactReader.GetString(3),
                        Email = contactReader.GetString(4)
                    };

                    switch (contact.ContactType?.ToLowerInvariant())
                    {
                        case "receiving":
                            model.Property.ReceivingContact = contact;
                            break;
                        case "property":
                            model.Property.PropertyContact = contact;
                            break;
                        case "regional manager":
                            model.ManagementCompany.RegionalManager = contact;
                            break;
                        case "manager":
                            model.ManagementCompany.PropertyManager = contact;
                            break;
                        case "account payable":
                        case "accounts payable":
                            model.Billing.AccountsPayable = contact;
                            break;
                        case "ap contact":
                        case "alternate ap contact":
                            model.Billing.AlternateAPContact = contact;
                            break;
                    }
                }
                contactReader.Close();
            }

            //return PartialView("ViewRequestPartial", model);
            return model;
        }

        public IActionResult Submissions()
        {
            var submissions = new List<NewCustomerListItem>();

            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT ID, PropertyName, SubmittedBy, SubmittedDate, SalespersonName
                    FROM NewCustomerRequests
                    ORDER BY SubmittedDate DESC
                ", conn);

                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    submissions.Add(new NewCustomerListItem
                    {
                        ID = reader.GetInt32(0),
                        PropertyName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        SubmittedBy = reader.IsDBNull(2) ? "N/A" : reader.GetString(2),
                        SubmittedDate = reader.GetDateTime(3),
                        SalespersonName = reader.IsDBNull(4) ? "" : reader.GetString(4)
                    });
                }
            }

            return View("Submissions", submissions);
        }

        public IActionResult NewCustomer()
        {
            return View();
        }
                
        // POST: /CustomerOnboarding/Submit
        [HttpPost]
        public IActionResult Submit(NewCustomerFormRequest model)
        {            
            // ✅ Set SubmittedBy here — before validation
            model.SubmittedBy = HttpContext.Session.GetString("Username") ?? "system";
            model.SubmittedByUserId = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");


            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Form submission failed. Please check required fields.";
                return View("NewCustomer", model);
            }

            try
            {
                int newRequestId;
                var connStr = _configuration.GetConnectionString("SalesMetrics");

                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // 1. Insert into NewCustomerRequests
                    var cmd = new SqlCommand(@"
                        INSERT INTO NewCustomerRequests (
                            SubmittedDate, SubmittedBy, SubmittedByUserId,
                            PropertyName, CreditLine, ShipToName, ShipToAddress,
                            PropertyUnits, MgmtCompanyName, MgmtCompanyAddress,
                            RequiresPDFInvoices, RequiresSpectrumInvoices,
                            InvoiceEmail, InvoiceAddress, InvoiceCity, InvoiceState, InvoiceZip, InvoiceAttention,
                            ThirdPartyVendor, RequiresCustomerPO,
                            SalespersonName, EstimatedMonthlyRevenue, ARCreditLimit,
                            Terms, DiscountPercent, SpecialNotes
                        ) 
                        OUTPUT INSERTED.ID
                        VALUES (
                            @SubmittedDate, @SubmittedBy, @SubmittedByUserId,
                            @PropertyName, @CreditLine, @ShipToName, @ShipToAddress,
                            @PropertyUnits, @MgmtCompanyName, @MgmtCompanyAddress,
                            @RequiresPDFInvoices, @RequiresSpectrumInvoices,
                            @InvoiceEmail, @InvoiceAddress, @InvoiceCity, @InvoiceState, @InvoiceZip, @InvoiceAttention,
                            @ThirdPartyVendor, @RequiresCustomerPO,
                            @SalespersonName, @EstimatedMonthlyRevenue, @ARCreditLimit,
                            @Terms, @DiscountPercent, @SpecialNotes
                    )", conn);


                    cmd.Parameters.AddWithValue("@SubmittedDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@SubmittedBy", model.SubmittedBy ?? "system");
                    cmd.Parameters.AddWithValue("@SubmittedByUserId", model.SubmittedByUserId);

                    // Property Info
                    cmd.Parameters.AddWithValue("@PropertyName", model.Property.Name);
                    cmd.Parameters.AddWithValue("@CreditLine", model.Property.CreditLine);
                    cmd.Parameters.AddWithValue("@ShipToName", model.Property.ShipToName);
                    cmd.Parameters.AddWithValue("@ShipToAddress", model.Property.ShipToAddress);
                    cmd.Parameters.AddWithValue("@PropertyUnits", model.Property.Units);

                    // Management
                    cmd.Parameters.AddWithValue("@MgmtCompanyName", model.ManagementCompany.ManagementName);
                    cmd.Parameters.AddWithValue("@MgmtCompanyAddress", model.ManagementCompany.ManagementAddress);
                    
                    // Billing
                    cmd.Parameters.AddWithValue("@RequiresPDFInvoices", model.Billing.RequiresPDFInvoices);
                    cmd.Parameters.AddWithValue("@RequiresSpectrumInvoices", model.Billing.RequiresSpectrumInvoices);
                    cmd.Parameters.AddWithValue("@InvoiceEmail", model.Billing.InvoiceEmail);
                    cmd.Parameters.AddWithValue("@InvoiceAddress", model.Billing.InvoiceAddress);
                    cmd.Parameters.AddWithValue("@InvoiceCity", model.Billing.InvoiceCity);
                    cmd.Parameters.AddWithValue("@InvoiceState", model.Billing.InvoiceState);
                    cmd.Parameters.AddWithValue("@InvoiceZip", model.Billing.InvoiceZip);
                    cmd.Parameters.AddWithValue("@InvoiceAttention", model.Billing.InvoiceAttention);
                    cmd.Parameters.AddWithValue("@ThirdPartyVendor", model.Billing.ThirdPartyVendor);
                    cmd.Parameters.AddWithValue("@RequiresCustomerPO", model.Billing.RequiresCustomerPO);

                    // Income
                    cmd.Parameters.AddWithValue("@SalespersonName", model.Income.SalespersonName);
                    cmd.Parameters.AddWithValue("@EstimatedMonthlyRevenue", model.Income.EstimatedMonthlyRevenue);
                    cmd.Parameters.AddWithValue("@ARCreditLimit", model.Income.ARCreditLimit);
                    cmd.Parameters.AddWithValue("@Terms", model.Income.Terms ?? "");
                    cmd.Parameters.AddWithValue("@DiscountPercent", model.Income.DiscountPercent ?? "");

                    // Notes
                    cmd.Parameters.AddWithValue("@SpecialNotes", model.SpecialNotes ?? "");

                    newRequestId = (int)cmd.ExecuteScalar();

                    // 2. Insert Contact records
                    var contacts = new List<Contact>
                    {
                        model.Property.ReceivingContact,
                        model.Property.PropertyContact,
                        model.ManagementCompany.RegionalManager,
                        model.ManagementCompany.PropertyManager,
                        model.Billing.AccountsPayable,
                        model.Billing.AlternateAPContact
                    };

                    foreach (var contact in contacts)
                    {
                        if (string.IsNullOrWhiteSpace(contact.Name)) continue; // Skip empty

                        var contactCmd = new SqlCommand(@"
                            INSERT INTO NewCustomerContacts (
                                NewCustomerRequestID, ContactType, Name, Phone, Fax, Email
                            ) VALUES (
                                @RequestID, @ContactType, @Name, @Phone, @Fax, @Email
                        )", conn);

                        contactCmd.Parameters.AddWithValue("@RequestID", newRequestId);
                        contactCmd.Parameters.AddWithValue("@ContactType", contact.ContactType ?? "Unknown");
                        contactCmd.Parameters.AddWithValue("@Name", contact.Name ?? "");
                        contactCmd.Parameters.AddWithValue("@Phone", contact.Phone ?? "");
                        contactCmd.Parameters.AddWithValue("@Fax", contact.Fax ?? "");
                        contactCmd.Parameters.AddWithValue("@Email", contact.Email ?? "");

                        contactCmd.ExecuteNonQuery();
                    }


                    // 3. Insert product line items
                    if (model.Products != null && model.Products.Any())
                    {
                        foreach (var p in model.Products)
                        {
                            var productCmd = new SqlCommand(@"
                                INSERT INTO NewCustomerProducts (
                                    NewCustomerRequestID, ProductClass, Style, Color, Price
                                ) VALUES (
                                    @RequestID, @ProductClass, @Style, @Color, @Price
                                )", conn);

                            productCmd.Parameters.AddWithValue("@RequestID", newRequestId);
                            productCmd.Parameters.AddWithValue("@ProductClass", p.ProductClass ?? "");
                            productCmd.Parameters.AddWithValue("@Style", p.Style ?? "");
                            productCmd.Parameters.AddWithValue("@Color", p.Color ?? "");
                            productCmd.Parameters.AddWithValue("@Price", p.Price);

                            productCmd.ExecuteNonQuery();
                        }
                    }
                }

                TempData["Success"] = "New Customer Request submitted successfully.";
                return RedirectToAction("NewCustomer");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error submitting request: " + ex.Message;
                return View("NewCustomer", model);
            }
        }

        public IActionResult PrintCustomerRequest(int id)
        {
            var model = GetCustomerRequestById(id); // Copy code from ViewRequestPartial logic
            return PartialView("_PrintCustomerRequest", model);
        }

        // USE SERVER-SIDE PDF GENERATION
        public IActionResult ExportCustomerRequestPdf(int id)
        {
            var model = GetCustomerRequestById(id);
            return new Rotativa.AspNetCore.ViewAsPdf("_PrintCustomerRequest", model)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                FileName = $"CustomerRequest_{id}.pdf"
            };
        }
    }
}
