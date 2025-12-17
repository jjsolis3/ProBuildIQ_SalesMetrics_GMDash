using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesMetrics.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSignTemplateHasDataMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SignTemplate",
                keyColumn: "TemplateKey",
                keyValue: "TenantConsentV1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SignTemplate",
                columns: new[] { "TemplateKey", "CreatedByUsers_ID", "CreatedDateUtc", "DefaultMessage", "DefaultSubject", "DisplayName", "IsActive", "MergeSpecJson", "ModifiedByUsers_ID", "ModifiedDateUtc", "RazorViewPath" },
                values: new object[] { "TenantConsentV1", 1, new DateTime(2025, 9, 25, 22, 35, 2, 152, DateTimeKind.Utc).AddTicks(2478), "Hi {{Recipient.FirstName}}, please review and sign to proceed.", "Please review and sign: Tenant Work Consent", "Tenant Work Consent (v1)", true, "{\r\n  \"merge\":\"property,order,customer\",\r\n  \"fields\":{\r\n    \"PropertyName\":\"ERP.Properties.Name\",\r\n    \"PropertyAddress\":\"ERP.Properties.Address\",\r\n    \"UnitNumber\":\"ERP.Orders.Unit\",\r\n    \"ManagerName\":\"ERP.PropertyManager.Name\",\r\n    \"TenantName\":\"ERP.Tenant.Name\",\r\n    \"OrderId\":\"ERP.Orders.Id\",\r\n    \"City\":\"ERP.Properties.City\",\r\n    \"State\":\"ERP.Properties.State\",\r\n    \"Zip\":\"ERP.Properties.Zip\"\r\n  }\r\n}", null, null, "/Views/SignTemplates/TenantConsent.cshtml" });
        }
    }
}
