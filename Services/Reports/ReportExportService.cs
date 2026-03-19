using System.Data;
using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

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

        /// <summary>
        /// Basic Excel export (no branding).
        /// </summary>
        public byte[] ExportToExcel(DataTable data, out string contentType)
        {
            return ExportToExcel(data, new ExcelExportOptions(), out contentType);
        }

        /// <summary>
        /// Branded Excel export with company info, logo, report title, and formatted data.
        /// </summary>
        public byte[] ExportToExcel(DataTable data, ExcelExportOptions options, out string contentType)
        {
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            using var stream = new MemoryStream();
            using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true);

            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            // Add stylesheet for formatting
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();
            stylesPart.Stylesheet.Save();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var mergeCells = new MergeCells();
            int colCount = Math.Max(data.Columns.Count, 4); // at least 4 columns for header
            string lastCol = GetColumnLetter(colCount - 1);

            uint rowIdx = 1;

            // === Company branding header ===
            bool hasCompany = !string.IsNullOrEmpty(options.CompanyName);
            uint brandingStartRow = rowIdx;

            if (hasCompany)
            {
                // Row 1: Company name (large bold, merged across all columns)
                var companyRow = CreateRow(rowIdx);
                companyRow.Append(CreateStyledCell($"A{rowIdx}", options.CompanyName!, 3));
                // Fill remaining cells so merge works
                for (int i = 1; i < colCount; i++)
                    companyRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
                sheetData.Append(companyRow);
                mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
                rowIdx++;

                // Row 2: Website | Phone
                var contactParts = new List<string>();
                if (!string.IsNullOrEmpty(options.CompanyWebsite)) contactParts.Add(options.CompanyWebsite);
                if (!string.IsNullOrEmpty(options.CompanyPhone)) contactParts.Add(options.CompanyPhone);
                if (contactParts.Any())
                {
                    var contactRow = CreateRow(rowIdx);
                    contactRow.Append(CreateStyledCell($"A{rowIdx}", string.Join("   |   ", contactParts), 1));
                    for (int i = 1; i < colCount; i++)
                        contactRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
                    sheetData.Append(contactRow);
                    mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
                    rowIdx++;
                }

                // Row 3: "SalesMetrics Report Builder" tagline
                var tagRow = CreateRow(rowIdx);
                tagRow.Append(CreateStyledCell($"A{rowIdx}", "SalesMetrics Report Builder", 6));
                for (int i = 1; i < colCount; i++)
                    tagRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
                sheetData.Append(tagRow);
                mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
                rowIdx++;

                // Blank row
                sheetData.Append(CreateRow(rowIdx++));
            }

            // === Report title section ===
            // Report name (bold, merged)
            var titleRow = CreateRow(rowIdx);
            titleRow.Append(CreateStyledCell($"A{rowIdx}", options.ReportName, 2));
            for (int i = 1; i < colCount; i++)
                titleRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
            sheetData.Append(titleRow);
            mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
            rowIdx++;

            // Description (if any)
            if (!string.IsNullOrEmpty(options.ReportDescription))
            {
                var descRow = CreateRow(rowIdx);
                descRow.Append(CreateStyledCell($"A{rowIdx}", options.ReportDescription, 1));
                for (int i = 1; i < colCount; i++)
                    descRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
                sheetData.Append(descRow);
                mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
                rowIdx++;
            }

            // Generated date and row count
            var infoRow = CreateRow(rowIdx);
            infoRow.Append(CreateStyledCell($"A{rowIdx}",
                $"Generated: {DateTime.Now:MMMM dd, yyyy h:mm tt}   |   Total Rows: {data.Rows.Count:N0}", 6));
            for (int i = 1; i < colCount; i++)
                infoRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
            sheetData.Append(infoRow);
            mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
            rowIdx++;

            // Blank row before data
            sheetData.Append(CreateRow(rowIdx++));

            // === Column headers (green background, white text, bold) ===
            var headerRow = CreateRow(rowIdx);
            for (int i = 0; i < data.Columns.Count; i++)
            {
                headerRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", data.Columns[i].ColumnName, 4));
            }
            sheetData.Append(headerRow);
            uint headerRowIdx = rowIdx;
            rowIdx++;

            // === Data rows ===
            foreach (DataRow dataRow in data.Rows)
            {
                var row = CreateRow(rowIdx);
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    var rawValue = dataRow[data.Columns[i]];
                    var cellRef = $"{GetColumnLetter(i)}{rowIdx}";

                    if (rawValue == null || rawValue == DBNull.Value)
                    {
                        row.Append(CreateStyledCell(cellRef, "", 5));
                    }
                    else if (IsNumericType(rawValue.GetType()))
                    {
                        row.Append(CreateNumberCell(cellRef, Convert.ToDouble(rawValue, CultureInfo.InvariantCulture), 7));
                    }
                    else if (rawValue is DateTime dt)
                    {
                        row.Append(CreateStyledCell(cellRef, dt.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture), 5));
                    }
                    else
                    {
                        row.Append(CreateStyledCell(cellRef,
                            Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? "", 5));
                    }
                }
                sheetData.Append(row);
                rowIdx++;
            }

            // === Build the worksheet ===
            var columns = new Columns();
            for (int i = 0; i < data.Columns.Count; i++)
            {
                // Auto-width: estimate based on header length (min 12, max 40)
                double width = Math.Max(12, Math.Min(40, data.Columns[i].ColumnName.Length * 1.3 + 4));
                columns.Append(new Column
                {
                    Min = (uint)(i + 1),
                    Max = (uint)(i + 1),
                    Width = width,
                    CustomWidth = true
                });
            }

            var worksheet = new Worksheet();
            worksheet.Append(columns);
            worksheet.Append(sheetData);
            if (mergeCells.HasChildren)
                worksheet.Append(mergeCells);

            // === Logo image ===
            if (!string.IsNullOrEmpty(options.LogoFilePath) && File.Exists(options.LogoFilePath))
            {
                try
                {
                    AddLogoImage(worksheetPart, worksheet, options.LogoFilePath, brandingStartRow);
                }
                catch
                {
                    // Logo embedding failed — continue without it
                }
            }

            // Auto-filter on header row
            worksheet.Append(new AutoFilter { Reference = $"A{headerRowIdx}:{GetColumnLetter(data.Columns.Count - 1)}{rowIdx - 1}" });

            worksheetPart.Worksheet = worksheet;

            // Add sheet to workbook
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = TruncateSheetName(options.ReportName)
            });

            workbookPart.Workbook.Save();
            document.Dispose();

            return stream.ToArray();
        }

        /// <summary>
        /// Exports multiple DataTables into a single .xlsx workbook, one sheet per entry.
        /// A compact branding header (company name + report name) is added to each sheet.
        /// </summary>
        public byte[] ExportToExcelMultiSheet(IList<(string SheetName, DataTable Data)> sheets,
                                               ExcelExportOptions options, out string contentType)
        {
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            using var stream = new MemoryStream();
            using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true);

            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();
            stylesPart.Stylesheet.Save();

            var sheetsElem = workbookPart.Workbook.AppendChild(new Sheets());

            for (int sheetIdx = 0; sheetIdx < sheets.Count; sheetIdx++)
            {
                var (sheetName, data) = sheets[sheetIdx];
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                var mergeCells = new MergeCells();
                int colCount = Math.Max(data.Columns.Count, 4);
                string lastCol = GetColumnLetter(colCount - 1);
                uint rowIdx = 1;

                // Compact branding: company name row
                if (!string.IsNullOrEmpty(options.CompanyName))
                {
                    var companyRow = CreateRow(rowIdx);
                    companyRow.Append(CreateStyledCell($"A{rowIdx}", options.CompanyName!, 3));
                    for (int i = 1; i < colCount; i++)
                        companyRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
                    sheetData.Append(companyRow);
                    mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
                    rowIdx++;
                }

                // Report name + sheet name (branch)
                var titleRow = CreateRow(rowIdx);
                var titleText = string.IsNullOrEmpty(options.ReportName)
                    ? sheetName
                    : $"{options.ReportName} — {sheetName}";
                titleRow.Append(CreateStyledCell($"A{rowIdx}", titleText, 2));
                for (int i = 1; i < colCount; i++)
                    titleRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
                sheetData.Append(titleRow);
                mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
                rowIdx++;

                // Generated date / row count
                var infoRow = CreateRow(rowIdx);
                infoRow.Append(CreateStyledCell($"A{rowIdx}",
                    $"Generated: {DateTime.Now:MMMM dd, yyyy h:mm tt}   |   Total Rows: {data.Rows.Count:N0}", 6));
                for (int i = 1; i < colCount; i++)
                    infoRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", "", 0));
                sheetData.Append(infoRow);
                mergeCells.Append(new MergeCell { Reference = $"A{rowIdx}:{lastCol}{rowIdx}" });
                rowIdx++;

                // Blank row
                sheetData.Append(CreateRow(rowIdx++));

                // Column headers
                var headerRow = CreateRow(rowIdx);
                for (int i = 0; i < data.Columns.Count; i++)
                    headerRow.Append(CreateStyledCell($"{GetColumnLetter(i)}{rowIdx}", data.Columns[i].ColumnName, 4));
                sheetData.Append(headerRow);
                uint headerRowIdx = rowIdx;
                rowIdx++;

                // Data rows
                foreach (DataRow dataRow in data.Rows)
                {
                    var row = CreateRow(rowIdx);
                    for (int i = 0; i < data.Columns.Count; i++)
                    {
                        var rawValue = dataRow[data.Columns[i]];
                        var cellRef = $"{GetColumnLetter(i)}{rowIdx}";
                        if (rawValue == null || rawValue == DBNull.Value)
                            row.Append(CreateStyledCell(cellRef, "", 5));
                        else if (IsNumericType(rawValue.GetType()))
                            row.Append(CreateNumberCell(cellRef, Convert.ToDouble(rawValue, CultureInfo.InvariantCulture), 7));
                        else if (rawValue is DateTime dt)
                            row.Append(CreateStyledCell(cellRef, dt.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture), 5));
                        else
                            row.Append(CreateStyledCell(cellRef, Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? "", 5));
                    }
                    sheetData.Append(row);
                    rowIdx++;
                }

                // Columns widths
                var columns = new Columns();
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    double width = Math.Max(12, Math.Min(40, data.Columns[i].ColumnName.Length * 1.3 + 4));
                    columns.Append(new Column { Min = (uint)(i + 1), Max = (uint)(i + 1), Width = width, CustomWidth = true });
                }

                var worksheet = new Worksheet();
                worksheet.Append(columns);
                worksheet.Append(sheetData);
                if (mergeCells.HasChildren)
                    worksheet.Append(mergeCells);
                if (data.Columns.Count > 0)
                    worksheet.Append(new AutoFilter { Reference = $"A{headerRowIdx}:{GetColumnLetter(data.Columns.Count - 1)}{rowIdx - 1}" });

                worksheetPart.Worksheet = worksheet;

                sheetsElem.Append(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = (uint)(sheetIdx + 1),
                    Name = TruncateSheetName(sheetName)
                });
            }

            workbookPart.Workbook.Save();
            document.Dispose();

            return stream.ToArray();
        }

        // ──────────────────────────────────────────────
        //  Logo image embedding
        // ──────────────────────────────────────────────
        private static void AddLogoImage(WorksheetPart worksheetPart, Worksheet worksheet,
                                         string logoPath, uint startRow)
        {
            var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();

            // Determine image type from extension
            var ext = Path.GetExtension(logoPath).ToLowerInvariant();
            var imageType = ext switch
            {
                ".jpg" or ".jpeg" => ImagePartType.Jpeg,
                ".gif" => ImagePartType.Gif,
                ".bmp" => ImagePartType.Bmp,
                _ => ImagePartType.Png
            };

            var imagePart = drawingsPart.AddImagePart(imageType);
            using (var fs = new FileStream(logoPath, FileMode.Open, FileAccess.Read))
            {
                imagePart.FeedData(fs);
            }
            var imageRelId = drawingsPart.GetIdOfPart(imagePart);

            // EMU units: 1 inch = 914400 EMU
            // Logo: roughly 2 inches wide x 0.6 inches tall
            long logoWidthEmu = 1828800;  // ~2 inches
            long logoHeightEmu = 548640;  // ~0.6 inches

            // Build the drawing with a OneCellAnchor so it floats without affecting cell layout
            var wsDrawing = new Xdr.WorksheetDrawing();
            wsDrawing.AddNamespaceDeclaration("xdr", "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing");
            wsDrawing.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

            var anchor = new Xdr.OneCellAnchor();

            // Position: column 0, row (startRow - 1 for 0-based)
            anchor.Append(new Xdr.FromMarker(
                new Xdr.ColumnId("0"),
                new Xdr.ColumnOffset("50000"),
                new Xdr.RowId((startRow - 1).ToString()),
                new Xdr.RowOffset("50000")
            ));

            anchor.Append(new Xdr.Extent { Cx = logoWidthEmu, Cy = logoHeightEmu });

            var picture = new Xdr.Picture();

            var nvPicPr = new Xdr.NonVisualPictureProperties(
                new Xdr.NonVisualDrawingProperties { Id = 1, Name = "Company Logo" },
                new Xdr.NonVisualPictureDrawingProperties(
                    new A.PictureLocks { NoChangeAspect = true }
                )
            );
            picture.Append(nvPicPr);

            var blipFill = new Xdr.BlipFill(
                new A.Blip { Embed = imageRelId, CompressionState = A.BlipCompressionValues.Print },
                new A.Stretch(new A.FillRectangle())
            );
            picture.Append(blipFill);

            var spPr = new Xdr.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = logoWidthEmu, Cy = logoHeightEmu }
                ),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }
            );
            picture.Append(spPr);

            anchor.Append(picture);
            anchor.Append(new Xdr.ClientData());

            wsDrawing.Append(anchor);
            drawingsPart.WorksheetDrawing = wsDrawing;
            drawingsPart.WorksheetDrawing.Save();

            // Link the drawing to the worksheet
            worksheet.Append(new Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
        }

        // ──────────────────────────────────────────────
        //  Stylesheet (fonts, fills, borders, formats)
        // ──────────────────────────────────────────────
        private static Stylesheet CreateStylesheet()
        {
            // Font 0: Default (11pt Calibri)
            // Font 1: Normal text (11pt Calibri) — same as default
            // Font 2: Bold title (13pt Calibri Bold)
            // Font 3: Company name (16pt Calibri Bold, dark green)
            // Font 4: Header (11pt Calibri Bold, white)
            // Font 5: Data number (11pt Calibri)
            // Font 6: Subtle (10pt Calibri, gray)

            var fonts = new Fonts(
                new Font( // 0 - Default
                    new FontSize { Val = 11 },
                    new FontName { Val = "Calibri" },
                    new Color { Theme = 1 }
                ),
                new Font( // 1 - Normal
                    new FontSize { Val = 11 },
                    new FontName { Val = "Calibri" },
                    new Color { Theme = 1 }
                ),
                new Font( // 2 - Bold title
                    new Bold(),
                    new FontSize { Val = 13 },
                    new FontName { Val = "Calibri" },
                    new Color { Rgb = "FF1A1A2E" }
                ),
                new Font( // 3 - Company name
                    new Bold(),
                    new FontSize { Val = 16 },
                    new FontName { Val = "Calibri" },
                    new Color { Rgb = "FF1B5E20" }
                ),
                new Font( // 4 - Header (white bold)
                    new Bold(),
                    new FontSize { Val = 11 },
                    new FontName { Val = "Calibri" },
                    new Color { Rgb = "FFFFFFFF" }
                ),
                new Font( // 5 - Data number
                    new FontSize { Val = 11 },
                    new FontName { Val = "Calibri" },
                    new Color { Theme = 1 }
                ),
                new Font( // 6 - Subtle gray
                    new FontSize { Val = 10 },
                    new FontName { Val = "Calibri" },
                    new Color { Rgb = "FF6C757D" }
                )
            )
            { Count = 7 };

            // Fill 0: None (required)
            // Fill 1: Gray125 (required)
            // Fill 2: Green header background
            var fills = new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),       // 0
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),    // 1
                new Fill(new PatternFill(                                            // 2 - Header green
                    new ForegroundColor { Rgb = "FF1A1A2E" }
                )
                { PatternType = PatternValues.Solid })
            )
            { Count = 3 };

            // Border 0: None
            // Border 1: Thin all sides (for data cells)
            var borders = new Borders(
                new Border(                                                          // 0 - None
                    new LeftBorder(), new RightBorder(), new TopBorder(), new BottomBorder(), new DiagonalBorder()
                ),
                new Border(                                                          // 1 - Thin all
                    new LeftBorder(new Color { Rgb = "FFD0D0D0" }) { Style = BorderStyleValues.Thin },
                    new RightBorder(new Color { Rgb = "FFD0D0D0" }) { Style = BorderStyleValues.Thin },
                    new TopBorder(new Color { Rgb = "FFD0D0D0" }) { Style = BorderStyleValues.Thin },
                    new BottomBorder(new Color { Rgb = "FFD0D0D0" }) { Style = BorderStyleValues.Thin },
                    new DiagonalBorder()
                )
            )
            { Count = 2 };

            var cellFormats = new CellFormats(
                new CellFormat { FontId = 0, FillId = 0, BorderId = 0 },                  // 0 - Default
                new CellFormat { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true }, // 1 - Normal text
                new CellFormat { FontId = 2, FillId = 0, BorderId = 0, ApplyFont = true }, // 2 - Bold title
                new CellFormat { FontId = 3, FillId = 0, BorderId = 0, ApplyFont = true }, // 3 - Company name
                new CellFormat { FontId = 4, FillId = 2, BorderId = 1,                     // 4 - Header cell
                    ApplyFont = true, ApplyFill = true, ApplyBorder = true,
                    ApplyAlignment = true,
                    Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center }
                },
                new CellFormat { FontId = 0, FillId = 0, BorderId = 1,                     // 5 - Data text
                    ApplyBorder = true },
                new CellFormat { FontId = 6, FillId = 0, BorderId = 0, ApplyFont = true }, // 6 - Subtle gray
                new CellFormat { FontId = 5, FillId = 0, BorderId = 1,                     // 7 - Data number
                    ApplyBorder = true, ApplyNumberFormat = true, NumberFormatId = 4 }      //     #,##0.00
            )
            { Count = 8 };

            return new Stylesheet(fonts, fills, borders, cellFormats);
        }

        // ──────────────────────────────────────────────
        //  Cell helpers
        // ──────────────────────────────────────────────

        private static Row CreateRow(uint rowIndex)
        {
            return new Row { RowIndex = rowIndex };
        }

        private static Cell CreateStyledCell(string cellRef, string text, uint styleIndex)
        {
            return new Cell
            {
                CellReference = cellRef,
                DataType = new EnumValue<CellValues>(CellValues.String),
                CellValue = new CellValue(SanitizeXmlString(text)),
                StyleIndex = styleIndex
            };
        }

        /// <summary>
        /// Removes characters that are illegal in XML 1.0, which would corrupt the .xlsx file.
        /// Illegal ranges: U+0000–U+0008, U+000B, U+000C, U+000E–U+001F, U+FFFE, U+FFFF.
        /// </summary>
        private static string SanitizeXmlString(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == 0x09 || c == 0x0A || c == 0x0D ||
                    (c >= 0x20 && c <= 0xD7FF) ||
                    (c >= 0xE000 && c <= 0xFFFD))
                {
                    sb.Append(c);
                }
                // else: illegal XML character — skip it
            }
            return sb.ToString();
        }

        private static Cell CreateNumberCell(string cellRef, double value, uint styleIndex)
        {
            return new Cell
            {
                CellReference = cellRef,
                DataType = new EnumValue<CellValues>(CellValues.Number),
                CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)),
                StyleIndex = styleIndex
            };
        }

        private static Cell CreateTextCell(string text)
        {
            return new Cell
            {
                DataType = new EnumValue<CellValues>(CellValues.String),
                CellValue = new CellValue(text)
            };
        }

        // ──────────────────────────────────────────────
        //  Utility helpers
        // ──────────────────────────────────────────────

        private static string GetColumnLetter(int columnIndex)
        {
            var result = new StringBuilder();
            while (columnIndex >= 0)
            {
                result.Insert(0, (char)('A' + (columnIndex % 26)));
                columnIndex = (columnIndex / 26) - 1;
            }
            return result.ToString();
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short)
                || type == typeof(decimal) || type == typeof(double) || type == typeof(float)
                || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong);
        }

        private static string TruncateSheetName(string name)
        {
            // Excel sheet names max 31 characters, no special chars
            var clean = name.Replace("[", "").Replace("]", "").Replace(":", "")
                           .Replace("*", "").Replace("?", "").Replace("/", "")
                           .Replace("\\", "");
            return clean.Length > 31 ? clean.Substring(0, 31) : clean;
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
