using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesMetrics.Migrations
{
    /// <inheritdoc />
    public partial class MoveSignTemplateSeedToMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SignTemplate",
                columns: new[] {
                    "TemplateKey","DisplayName","RazorViewPath","MergeSpecJson",
                    "DefaultSubject","DefaultMessage","IsActive","CreatedByUsers_ID","CreatedDateUtc"
                },
                values: new object[] {
                    "TenantConsentV1",
                    "Tenant Work Consent (v1)",
                    "/Views/SignTemplates/TenantConsent.cshtml",
                    // keep this JSON identical to what you had before:
                    "{\r\n  \"merge\":\"property,order,customer\",\r\n  \"fields\":{\r\n    \"PropertyName\":\"ERP.Properties.Name\",\r\n    \"PropertyAddress\":\"ERP.Properties.Address\",\r\n    \"UnitNumber\":\"ERP.Orders.Unit\",\r\n    \"ManagerName\":\"ERP.PropertyManager.Name\",\r\n    \"TenantName\":\"ERP.Tenant.Name\",\r\n    \"OrderId\":\"ERP.Orders.Id\",\r\n    \"City\":\"ERP.Properties.City\",\r\n    \"State\":\"ERP.Properties.State\",\r\n    \"Zip\":\"ERP.Properties.Zip\"\r\n  }\r\n}",
                    "Please review and sign: Tenant Work Consent",
                    "Hi {{Recipient.FirstName}}, please review and sign to proceed.",
                    true,
                    1,
                    new DateTime(2025, 9, 25, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SignTemplate",
                keyColumn: "TemplateKey",
                keyValue: "TenantConsentV1");

        }
    }
}
