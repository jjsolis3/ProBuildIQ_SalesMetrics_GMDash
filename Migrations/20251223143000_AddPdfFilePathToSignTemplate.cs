using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesMetrics.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfFilePathToSignTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PdfFilePath",
                table: "SignTemplate",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfFilePath",
                table: "SignTemplate");
        }
    }
}