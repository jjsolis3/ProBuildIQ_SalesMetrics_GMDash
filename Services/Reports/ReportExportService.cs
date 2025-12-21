using System.Data;
using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SalesMetrics.Services.Reports
{
    public class ReportExportService : IReportExportService
    {
        public byte[] ExportToCsv(DataTable data, out string contentType)
        {
            contentType = "text/csv";
            var builder = new StringBuilder();

            if (data.Columns.Count == 0)
            {
                return Encoding.UTF8.GetBytes(string.Empty);
            }

            // Header
            builder.AppendLine(string.Join(",", data.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName))));

            foreach (DataRow row in data.Rows)
            {
                var values = data.Columns.Cast<DataColumn>()
                    .Select(col => Escape(Convert.ToString(row[col], CultureInfo.InvariantCulture) ?? string.Empty));
                builder.AppendLine(string.Join(",", values));
            }

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        public byte[] ExportToExcel(DataTable data, out string contentType)
        {
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            using var stream = new MemoryStream();
            using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true);
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            // Header row
            var headerRow = new Row();
            foreach (DataColumn column in data.Columns)
            {
                headerRow.Append(CreateTextCell(column.ColumnName));
            }
            sheetData.Append(headerRow);

            // Data rows
            foreach (DataRow row in data.Rows)
            {
                var newRow = new Row();
                foreach (DataColumn column in data.Columns)
                {
                    var value = Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? string.Empty;
                    newRow.Append(CreateTextCell(value));
                }

                sheetData.Append(newRow);
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = document.WorkbookPart?.Workbook.AppendChild(new Sheets());
            sheets?.Append(new Sheet
            {
                Id = document.WorkbookPart?.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Report"
            });

            workbookPart.Workbook.Save();

            return stream.ToArray();
        }

        private static Cell CreateTextCell(string text)
        {
            return new Cell
            {
                DataType = new EnumValue<CellValues>(CellValues.String),
                CellValue = new CellValue(text)
            };
        }

        private static string Escape(string value)
        {
            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }

            if (value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                value = $"\"{value}\"";
            }

            return value;
        }
    }
}
