using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SalesMetrics.Data;
using SalesMetrics.Data.Entities.QueryBuilder;
using SalesMetrics.Services.Permissions;
using SalesMetrics.Models.QueryBuilder;
using Microsoft.EntityFrameworkCore;

namespace SalesMetrics.Controllers
{
    [Authorize]
    public class ReportBuilderController : Controller
    {
        private readonly SalesMetricsDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<ReportBuilderController> _logger;
        private readonly IConfiguration _configuration;

        public ReportBuilderController(
            SalesMetricsDbContext context,
            IPermissionService permissionService,
            ILogger<ReportBuilderController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _permissionService = permissionService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Display list of all custom reports (Query Builder Index page)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();

            // Check if user has Query Builder access
            var hasAccess = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ACCESS");
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted to access Query Builder without permission", userId);
                return Forbid();
            }

            var currentLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
            var roleId = HttpContext.Session.GetString("RoleId") ?? "0";

            // Get all reports the user can access
            var reports = await _context.ReportDefinitions
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.ModifiedDate ?? r.CreatedDate)
                .ToListAsync();

            // TODO: Filter reports based on user's role and location
            // For now, show all active reports

            var viewModel = new ReportBuilderIndexViewModel
            {
                Reports = reports.Select(r => new ReportSummaryDto
                {
                    ReportId = r.ReportDefinitionId,
                    Name = r.Name ?? "Untitled Report",
                    Description = r.Description ?? "",
                    DataSourceType = r.DataSourceType ?? "SQL",
                    CreatedBy = "User " + r.CreatedByUserId, // TODO: Join with Users table to get name
                    CreatedDate = r.CreatedDate,
                    LastModifiedDate = r.ModifiedDate ?? r.CreatedDate,
                    IsShared = !string.IsNullOrEmpty(r.AllowedRoles) || !string.IsNullOrEmpty(r.AllowedLocations)
                }).ToList(),
                CanCreateReports = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_CREATE"),
                CanEditReports = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_EDIT"),
                CanDeleteReports = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_DELETE"),
                IsAdmin = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ADMIN")
            };

            return View(viewModel);
        }

        /// <summary>
        /// Show create report wizard - Step 1: Table Selection
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();

            var canCreate = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_CREATE");
            if (!canCreate)
            {
                _logger.LogWarning("User {UserId} attempted to create report without permission", userId);
                return Forbid();
            }

            // Load available tables from whitelist
            var allowedTables = await _context.AllowedTables
                .Where(t => t.IsActive)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.TableName)
                .ToListAsync();

            // Build view model for Step 1
            var viewModel = new ReportBuilderCreateViewModel
            {
                CurrentStep = 1,
                TotalSteps = 5,
                AvailableTables = allowedTables.Select(t => new TableSelectionItem
                {
                    TableId = t.AllowedTableId,
                    TableName = t.TableName ?? "",
                    SchemaName = t.SchemaName ?? "dbo",
                    DisplayName = t.DisplayName ?? t.TableName ?? "",
                    Description = t.Description ?? "",
                    Category = t.Category ?? "Uncategorized",
                    DataSourceType = t.DataSourceType ?? "SQL",
                    IsSelected = false
                }).ToList(),
                CanShare = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_SHARE"),
                IsAdmin = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ADMIN")
            };

            _logger.LogInformation("User {UserId} started creating a new report", userId);

            return View(viewModel);
        }

        /// <summary>
        /// Process Step 1 (Table Selection) and advance to Step 2 (Column Configuration)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateStep1([FromBody] Step1SubmissionDto model)
        {
            var userId = GetCurrentUserId();

            var canCreate = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_CREATE");
            if (!canCreate)
            {
                return Json(new { success = false, message = "You don't have permission to create reports" });
            }

            if (model.SelectedTableIds == null || !model.SelectedTableIds.Any())
            {
                return Json(new { success = false, message = "Please select at least one table" });
            }

            _logger.LogInformation("User {UserId} selected {Count} tables for new report", userId, model.SelectedTableIds.Count);

            // Get user's office location to determine which CompUFloor database to query
            var userLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
            var compuFloorDb = GetCompUFloorDatabaseName(userLocation);

            _logger.LogInformation("User location: {Location}, CompUFloor database: {Database}", userLocation, compuFloorDb);

            // Fetch the selected tables from database
            var selectedTables = await _context.AllowedTables
                .Where(t => model.SelectedTableIds.Contains(t.AllowedTableId))
                .ToListAsync();

            // Fetch columns for all selected tables from CompUFloor INFORMATION_SCHEMA
            var allColumns = new List<ColumnSelectionItem>();

            foreach (var table in selectedTables)
            {
                try
                {
                    // Use direct SQL connection to query CompUFloor INFORMATION_SCHEMA
                    var connection = _context.Database.GetDbConnection();
                    var wasOpen = connection.State == System.Data.ConnectionState.Open;

                    if (!wasOpen)
                    {
                        await connection.OpenAsync();
                    }

                    using (var command = connection.CreateCommand())
                    {
                        // Query CompUFloor database INFORMATION_SCHEMA using cross-database query
                        command.CommandText = $@"
                            SELECT
                                COLUMN_NAME,
                                DATA_TYPE,
                                IS_NULLABLE,
                                ORDINAL_POSITION
                            FROM [{compuFloorDb}].INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_SCHEMA = @schema
                            AND TABLE_NAME = @tableName
                            ORDER BY ORDINAL_POSITION";

                        var schemaParam = command.CreateParameter();
                        schemaParam.ParameterName = "@schema";
                        schemaParam.Value = table.SchemaName ?? "dbo";
                        command.Parameters.Add(schemaParam);

                        var tableParam = command.CreateParameter();
                        tableParam.ParameterName = "@tableName";
                        tableParam.Value = table.TableName;
                        command.Parameters.Add(tableParam);

                        _logger.LogInformation("Querying columns for table {Table} from database {Database}", table.TableName, compuFloorDb);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            int columnCount = 0;
                            while (await reader.ReadAsync())
                            {
                                var columnName = reader["COLUMN_NAME"].ToString() ?? "";
                                var dataType = reader["DATA_TYPE"].ToString() ?? "varchar";
                                var isNullable = reader["IS_NULLABLE"].ToString() == "YES";

                                allColumns.Add(new ColumnSelectionItem
                                {
                                    ColumnId = $"{table.TableName}.{columnName}",
                                    TableName = table.TableName ?? "",
                                    TableDisplayName = table.DisplayName ?? table.TableName ?? "",
                                    ColumnName = columnName,
                                    DisplayName = columnName,
                                    DataType = dataType,
                                    IsNullable = isNullable,
                                    IsSelected = false,
                                    SortOrder = null,
                                    SortDirection = "ASC",
                                    AggregateFunction = null
                                });
                                columnCount++;
                            }
                            _logger.LogInformation("Found {Count} columns for table {Table}", columnCount, table.TableName);
                        }
                    }

                    if (!wasOpen)
                    {
                        await connection.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching columns for table {TableName} from database {Database}", table.TableName, compuFloorDb);
                    return Json(new { success = false, message = $"Error fetching columns for table {table.TableName}: {ex.Message}" });
                }
            }

            _logger.LogInformation("Total columns fetched: {Count}", allColumns.Count);

            // Check if multiple tables selected - if yes, go to Step 1.5 (Relationships)
            if (selectedTables.Count > 1)
            {
                // Build list of tables with their columns for relationship configuration
                var tablesWithColumns = selectedTables.Select(t => new TableWithColumns
                {
                    TableName = t.TableName ?? "",
                    DisplayName = t.DisplayName ?? t.TableName ?? "",
                    Columns = allColumns
                        .Where(c => c.TableName == t.TableName)
                        .Select(c => new TableColumnInfo
                        {
                            ColumnName = c.ColumnName,
                            DataType = c.DataType
                        })
                        .ToList()
                }).ToList();

                // Store wizard state for Step 1.5
                TempData["WizardState"] = System.Text.Json.JsonSerializer.Serialize(new
                {
                    CurrentStep = 1.5,
                    SelectedTableIds = model.SelectedTableIds,
                    SelectedTables = selectedTables.Select(t => new
                    {
                        t.AllowedTableId,
                        t.TableName,
                        t.DisplayName,
                        t.Category
                    }).ToList(),
                    AllColumns = allColumns
                });

                return Json(new
                {
                    success = true,
                    message = "Tables selected successfully",
                    nextStep = 1.5,
                    tablesWithColumns = tablesWithColumns
                });
            }
            else
            {
                // Single table selected - skip relationships, go straight to Step 2
                TempData["WizardState"] = System.Text.Json.JsonSerializer.Serialize(new
                {
                    CurrentStep = 2,
                    SelectedTableIds = model.SelectedTableIds,
                    SelectedTables = selectedTables.Select(t => new
                    {
                        t.AllowedTableId,
                        t.TableName,
                        t.DisplayName,
                        t.Category
                    }).ToList()
                });

                return Json(new
                {
                    success = true,
                    message = "Tables selected successfully",
                    nextStep = 2,
                    columns = allColumns
                });
            }
        }

        /// <summary>
        /// Get CompUFloor database name based on office location
        /// </summary>
        private string GetCompUFloorDatabaseName(string location)
        {
            return location.ToUpper() switch
            {
                "LAX" => "CompUFloorLA",
                "LSV" => "CompUFloorLV",
                "CHN" => "CompUFloorChino",
                "PHX" => "CompUFloorPHX",
                "SND" => "CompUFloorSD",
                _ => "CompUFloorLA" // Default to LA if unknown
            };
        }

        /// <summary>
        /// Process Step 1.5 (Table Relationships) and advance to Step 2 (Column Configuration)
        /// </summary>
        [HttpPost]
        public IActionResult CreateStep1_5([FromBody] Step1_5SubmissionDto model)
        {
            try
            {
                _logger.LogInformation("CreateStep1_5 called");

                var userId = GetCurrentUserId();
                _logger.LogInformation("User ID: {UserId}", userId);

                var canCreate = _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_CREATE").Result;
                if (!canCreate)
                {
                    _logger.LogWarning("User {UserId} does not have permission", userId);
                    return Json(new { success = false, message = "You don't have permission to create reports" });
                }

                if (model?.Relationships == null || !model.Relationships.Any())
                {
                    _logger.LogWarning("No relationships provided in model");
                    return Json(new { success = false, message = "Please define at least one relationship between your tables" });
                }

                _logger.LogInformation("User {UserId} configured {Count} table relationships", userId, model.Relationships.Count);

                // Log each relationship
                foreach (var rel in model.Relationships)
                {
                    _logger.LogInformation("Relationship: {LeftTable}.{LeftColumn} {JoinType} {RightTable}.{RightColumn}",
                        rel.LeftTable, rel.LeftColumn, rel.JoinType, rel.RightTable, rel.RightColumn);
                }

                // Retrieve previously stored columns from TempData
                var wizardStateJson = TempData["WizardState"]?.ToString();
                _logger.LogInformation("WizardState JSON length: {Length}", wizardStateJson?.Length ?? 0);

                if (string.IsNullOrEmpty(wizardStateJson))
                {
                    _logger.LogError("WizardState is empty or null");
                    return Json(new { success = false, message = "Session expired. Please start over." });
                }

                // Parse JSON properly using JsonDocument
                List<ColumnSelectionItem> allColumns;
                try
                {
                    _logger.LogInformation("Parsing wizard state JSON");
                    using var document = System.Text.Json.JsonDocument.Parse(wizardStateJson);
                    var root = document.RootElement;

                    if (root.TryGetProperty("AllColumns", out var columnsElement))
                    {
                        var columnsJson = columnsElement.GetRawText();
                        _logger.LogInformation("AllColumns JSON length: {Length}", columnsJson.Length);

                        var jsonOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        allColumns = System.Text.Json.JsonSerializer.Deserialize<List<ColumnSelectionItem>>(columnsJson, jsonOptions) ?? new List<ColumnSelectionItem>();
                        _logger.LogInformation("Deserialized {Count} columns", allColumns.Count);
                    }
                    else
                    {
                        _logger.LogError("AllColumns property not found in wizard state");
                        return Json(new { success = false, message = "Session data is invalid. Please start over." });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing wizard state: {Message}", ex.Message);
                    return Json(new { success = false, message = $"Failed to retrieve session data: {ex.Message}" });
                }

                // Store wizard state for Step 2
                _logger.LogInformation("Storing wizard state for Step 2");
                TempData["WizardState_Step1_5"] = System.Text.Json.JsonSerializer.Serialize(new
                {
                    CurrentStep = 2,
                    Relationships = model.Relationships
                });

                _logger.LogInformation("CreateStep1_5 completed successfully");

                return Json(new
                {
                    success = true,
                    message = "Relationships configured successfully",
                    nextStep = 2,
                    columns = allColumns
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in CreateStep1_5: {Message}", ex.Message);
                return Json(new { success = false, message = $"Server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Process Step 2 (Column Configuration) and advance to Step 3 (Filter Configuration)
        /// </summary>
        [HttpPost]
        public IActionResult CreateStep2([FromBody] Step2SubmissionDto model)
        {
            var userId = GetCurrentUserId();

            var canCreate = _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_CREATE").Result;
            if (!canCreate)
            {
                return Json(new { success = false, message = "You don't have permission to create reports" });
            }

            if (model.SelectedColumns == null || !model.SelectedColumns.Any())
            {
                return Json(new { success = false, message = "Please select at least one column" });
            }

            _logger.LogInformation("User {UserId} selected {Count} columns for new report", userId, model.SelectedColumns.Count);

            // Store wizard state in TempData for next step
            TempData["WizardState_Step2"] = System.Text.Json.JsonSerializer.Serialize(new
            {
                CurrentStep = 3,
                SelectedColumns = model.SelectedColumns
            });

            return Json(new
            {
                success = true,
                message = "Columns configured successfully",
                nextStep = 3,
                selectedColumns = model.SelectedColumns
            });
        }

        /// <summary>
        /// Edit an existing report
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();

            var canEdit = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_EDIT");
            if (!canEdit)
            {
                _logger.LogWarning("User {UserId} attempted to edit report without permission", userId);
                return Forbid();
            }

            var report = await _context.ReportDefinitions
                .FirstOrDefaultAsync(r => r.ReportDefinitionId == id && r.IsActive);

            if (report == null)
            {
                _logger.LogWarning("Report {ReportId} not found or inactive", id);
                return NotFound();
            }

            _logger.LogInformation("User {UserId} editing report {ReportId}: {ReportName}", userId, id, report.Name);

            // Parse the query definition to determine mode and reconstruct state
            var viewModel = new ReportEditViewModel
            {
                ReportId = report.ReportDefinitionId,
                ReportName = report.Name ?? "",
                ReportDescription = report.Description ?? "",
                Category = report.Category ?? "Custom Reports",
                QueryMode = "sql", // Default to SQL mode
                CustomSql = report.GeneratedSql ?? "",
                QueryDefinitionJson = report.QueryDefinitionJson ?? ""
            };

            // Try to parse QueryDefinitionJson to determine actual mode
            if (!string.IsNullOrEmpty(report.QueryDefinitionJson))
            {
                try
                {
                    using var document = System.Text.Json.JsonDocument.Parse(report.QueryDefinitionJson);
                    var root = document.RootElement;

                    if (root.TryGetProperty("queryMode", out var modeElement))
                    {
                        viewModel.QueryMode = modeElement.GetString() ?? "sql";
                    }

                    if (root.TryGetProperty("customSql", out var sqlElement))
                    {
                        viewModel.CustomSql = sqlElement.GetString() ?? "";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not parse QueryDefinitionJson for report {ReportId}", id);
                }
            }

            return View(viewModel);
        }

        /// <summary>
        /// Update an existing report (POST from Edit form)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Update([FromBody] UpdateReportDto model)
        {
            try
            {
                var userId = GetCurrentUserId();

                var canEdit = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_EDIT");
                if (!canEdit)
                {
                    return Json(new { success = false, message = "You don't have permission to edit reports" });
                }

                // Load existing report
                var report = await _context.ReportDefinitions
                    .FirstOrDefaultAsync(r => r.ReportDefinitionId == model.ReportId && r.IsActive);

                if (report == null)
                {
                    return Json(new { success = false, message = "Report not found" });
                }

                _logger.LogInformation("User {UserId} updating report {ReportId}", userId, model.ReportId);

                // Validate required fields
                if (string.IsNullOrWhiteSpace(model.ReportName))
                {
                    return Json(new { success = false, message = "Report name is required" });
                }

                // Generate SQL and query definition based on mode
                string generatedSql;
                string queryDefJson;

                if (model.QueryMode == "sql")
                {
                    // SQL Mode - validate and use custom SQL
                    if (string.IsNullOrWhiteSpace(model.CustomSql))
                    {
                        return Json(new { success = false, message = "SQL query is required in SQL mode" });
                    }

                    var (isValid, errorMessage) = ValidateCustomSql(model.CustomSql);
                    if (!isValid)
                    {
                        return Json(new { success = false, message = $"SQL validation failed: {errorMessage}" });
                    }

                    generatedSql = model.CustomSql.Trim();

                    // Detect parameters
                    var parameters = DetectParametersFromSql(generatedSql);

                    queryDefJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        queryMode = "sql",
                        customSql = generatedSql,
                        parameters = parameters
                    });
                }
                else
                {
                    // Wizard Mode - generate SQL from selections
                    var userLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
                    var compuFloorDb = GetCompUFloorDatabaseName(userLocation);

                    // Create Step4SubmissionDto from the update model
                    var step4Model = new Step4SubmissionDto
                    {
                        QueryMode = "wizard",
                        Tables = model.Tables,
                        Relationships = model.Relationships,
                        Columns = model.Columns,
                        Filters = model.Filters
                    };

                    generatedSql = GenerateSQL(step4Model, compuFloorDb);

                    queryDefJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        queryMode = "wizard",
                        tables = model.Tables,
                        relationships = model.Relationships,
                        columns = model.Columns,
                        filters = model.Filters
                    });
                }

                // Update report properties
                report.Name = model.ReportName;
                report.Description = model.ReportDescription;
                report.Category = model.Category ?? "Custom Reports";
                report.QueryDefinitionJson = queryDefJson;
                report.GeneratedSql = generatedSql;
                report.ModifiedByUserId = userId;
                report.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Report {ReportId} updated successfully", report.ReportDefinitionId);

                return Json(new
                {
                    success = true,
                    message = "Report updated successfully!",
                    reportId = report.ReportDefinitionId,
                    reportName = report.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report");
                return Json(new
                {
                    success = false,
                    message = "Error updating report: " + ex.Message
                });
            }
        }

        /// <summary>
        /// Step 4: Preview Query Results
        /// Executes the query and returns preview data
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateStep4([FromBody] Step4SubmissionDto model)
        {
            try
            {
                _logger.LogInformation("Step 4: Previewing query in {Mode} mode", model.QueryMode);

                string sql;

                if (model.QueryMode == "sql")
                {
                    // SQL Mode - use custom SQL
                    if (string.IsNullOrWhiteSpace(model.CustomSql))
                    {
                        return Json(new { success = false, message = "Custom SQL query is required in SQL mode" });
                    }

                    // Validate SQL for security
                    var (isValid, errorMessage) = ValidateCustomSql(model.CustomSql);
                    if (!isValid)
                    {
                        return Json(new { success = false, message = $"SQL validation failed: {errorMessage}" });
                    }

                    sql = model.CustomSql.Trim();
                    _logger.LogInformation("Using custom SQL: {SQL}", sql);
                }
                else
                {
                    // Wizard Mode - generate SQL from selections
                    _logger.LogInformation("Generating SQL from {TableCount} tables, {ColumnCount} columns, {FilterCount} filters",
                        model.Tables?.Count ?? 0, model.Columns?.Count ?? 0, model.Filters?.Count ?? 0);

                    // Get user's office location and determine CompUFloor database
                    var userLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
                    var compuFloorDb = GetCompUFloorDatabaseName(userLocation);

                    _logger.LogInformation("User location: {Location}, CompUFloor database: {Database}", userLocation, compuFloorDb);

                    // Build SQL query with fully qualified table names
                    sql = GenerateSQL(model, compuFloorDb);
                    _logger.LogInformation("Generated SQL: {SQL}", sql);
                }

                // Execute query with limit
                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new System.Data.SqlClient.SqlConnection(connStr);
                await conn.OpenAsync();

                // For SQL mode, switch to the user's CompUFloor database so unqualified
                // table names (e.g. INVOICE_HEADER) resolve correctly.
                // Wizard mode already uses fully qualified [Database].[dbo].[Table] names.
                if (model.QueryMode == "sql")
                {
                    var userLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
                    var compuFloorDb = GetCompUFloorDatabaseName(userLocation);
                    _logger.LogInformation("SQL mode: switching connection to database {Database}", compuFloorDb);
                    await conn.ChangeDatabaseAsync(compuFloorDb);
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var cmd = new System.Data.SqlClient.SqlCommand(sql, conn);
                cmd.CommandTimeout = 30; // 30 seconds timeout

                var previewData = new List<Dictionary<string, object>>();
                var columnNames = new List<string>();

                using var reader = await cmd.ExecuteReaderAsync();

                // Get column names
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columnNames.Add(reader.GetName(i));
                }

                // Read up to 100 rows for preview
                int rowCount = 0;
                while (await reader.ReadAsync() && rowCount < 100)
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    previewData.Add(row);
                    rowCount++;
                }

                stopwatch.Stop();

                return Json(new
                {
                    success = true,
                    previewData,
                    columnNames,
                    rowCount = previewData.Count,
                    executionTimeMs = stopwatch.ElapsedMilliseconds,
                    generatedSql = sql,
                    hasMoreRows = rowCount == 100
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing query in Step 4");
                return Json(new
                {
                    success = false,
                    message = "Error executing query: " + ex.Message,
                    errorDetails = ex.ToString()
                });
            }
        }

        /// <summary>
        /// Step 5: Save Report Definition
        /// Persists the report to the database
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveReport([FromBody] SaveReportDto model)
        {
            try
            {
                var userId = GetCurrentUserId();

                _logger.LogInformation("Saving report '{ReportName}' for user {UserId} in {Mode} mode", model.ReportName, userId, model.QueryMode);

                // Validate
                if (string.IsNullOrWhiteSpace(model.ReportName))
                {
                    return Json(new { success = false, message = "Report name is required" });
                }

                string generatedSql;
                string queryDefJson;

                if (model.QueryMode == "sql")
                {
                    // SQL Mode - validate and use custom SQL
                    if (string.IsNullOrWhiteSpace(model.CustomSql))
                    {
                        return Json(new { success = false, message = "Custom SQL is required in SQL mode" });
                    }

                    var (isValid, errorMessage) = ValidateCustomSql(model.CustomSql);
                    if (!isValid)
                    {
                        return Json(new { success = false, message = $"SQL validation failed: {errorMessage}" });
                    }

                    generatedSql = model.CustomSql.Trim();

                    // Detect parameters in SQL
                    var parameters = DetectParametersFromSql(model.CustomSql);

                    // Store custom SQL and parameters in query definition
                    queryDefJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        queryMode = "sql",
                        customSql = model.CustomSql,
                        parameters = parameters
                    });

                    _logger.LogInformation("Detected {ParamCount} parameters in SQL: {ParamNames}",
                        parameters.Count, string.Join(", ", parameters.Select(p => p.Name)));
                }
                else
                {
                    // Wizard Mode - validate selections
                    if (model.Tables == null || !model.Tables.Any())
                    {
                        return Json(new { success = false, message = "At least one table must be selected" });
                    }

                    if (model.Columns == null || !model.Columns.Any())
                    {
                        return Json(new { success = false, message = "At least one column must be selected" });
                    }

                    // Get user's office location and determine CompUFloor database
                    var userLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
                    var compuFloorDb = GetCompUFloorDatabaseName(userLocation);

                    // Build query definition JSON
                    queryDefJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        queryMode = "wizard",
                        tables = model.Tables,
                        relationships = model.Relationships,
                        columns = model.Columns,
                        filters = model.Filters,
                        compuFloorDatabase = compuFloorDb
                    });

                    // Generate SQL with fully qualified table names
                    generatedSql = GenerateSQL(new Step4SubmissionDto
                    {
                        Tables = model.Tables,
                        Relationships = model.Relationships,
                        Columns = model.Columns,
                        Filters = model.Filters
                    }, compuFloorDb);
                }

                // Generate report ID
                var reportId = $"CUSTOM_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                // Create report definition
                var reportDef = new ReportDefinitionEntity
                {
                    ReportId = reportId,
                    Name = model.ReportName,
                    Description = model.ReportDescription,
                    Category = model.Category ?? "Custom Reports",
                    IsCustom = true,
                    QueryDefinitionJson = queryDefJson,
                    GeneratedSql = generatedSql,
                    DataSourceType = "SQL",
                    IsActive = true,
                    Version = 1,
                    CreatedByUserId = userId,
                    CreatedDate = DateTime.UtcNow,
                    IsScheduled = false
                };

                _context.ReportDefinitions.Add(reportDef);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Report saved successfully with ID {ReportId}", reportDef.ReportDefinitionId);

                return Json(new
                {
                    success = true,
                    message = "Report saved successfully!",
                    reportId = reportDef.ReportDefinitionId,
                    reportName = reportDef.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report");
                return Json(new
                {
                    success = false,
                    message = "Error saving report: " + ex.Message
                });
            }
        }

        /// <summary>
        /// Helper method to generate SQL from query definition
        /// </summary>
        private string GenerateSQL(Step4SubmissionDto model, string compuFloorDatabase)
        {
            var sql = new System.Text.StringBuilder();

            // Build a lookup from table name to alias using the Tables list
            var tableAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in model.Tables)
            {
                if (!tableAliasMap.ContainsKey(t.TableName))
                    tableAliasMap[t.TableName] = t.Alias;
            }

            // SELECT clause - Quote column names and aliases to handle spaces and special characters
            sql.AppendLine("SELECT");
            var columnExpressions = model.Columns.Select(c =>
            {
                // Use ColumnName as fallback if DisplayName is empty or whitespace
                var aliasName = string.IsNullOrWhiteSpace(c.DisplayName) ? c.ColumnName : c.DisplayName;
                return $"    {c.TableAlias}.[{c.ColumnName}] AS [{aliasName}]";
            });
            sql.AppendLine(string.Join(",\n", columnExpressions));

            // FROM clause - Use fully qualified table names to support cross-database queries
            var baseTable = model.Tables.FirstOrDefault(t => t.IsBaseTable == true);
            if (baseTable == null)
                baseTable = model.Tables.First(); // Use first table as base if none marked

            sql.AppendLine($"FROM [{compuFloorDatabase}].[dbo].[{baseTable.TableName}] AS {baseTable.Alias}");

            // JOIN clauses (from relationships)
            // Track which tables have already been added to the FROM/JOIN chain to avoid
            // emitting duplicate aliases (e.g. "T2" appearing twice).
            var joinedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseTable.TableName };

            if (model.Relationships != null && model.Relationships.Any())
            {
                foreach (var rel in model.Relationships)
                {
                    // Resolve aliases from the canonical table alias map
                    var fromAlias = tableAliasMap.GetValueOrDefault(rel.FromTable, rel.FromAlias);
                    var toAlias = tableAliasMap.GetValueOrDefault(rel.ToTable, rel.ToAlias);

                    if (!joinedTables.Contains(rel.ToTable))
                    {
                        // Normal case: first time this table appears, emit a JOIN
                        sql.AppendLine($"{rel.JoinType} JOIN [{compuFloorDatabase}].[dbo].[{rel.ToTable}] AS {toAlias} ON {fromAlias}.[{rel.FromColumn}] = {toAlias}.[{rel.ToColumn}]");
                        joinedTables.Add(rel.ToTable);
                    }
                    else if (!joinedTables.Contains(rel.FromTable))
                    {
                        // The "To" table is already joined but "From" is not — join the From table instead
                        sql.AppendLine($"{rel.JoinType} JOIN [{compuFloorDatabase}].[dbo].[{rel.FromTable}] AS {fromAlias} ON {fromAlias}.[{rel.FromColumn}] = {toAlias}.[{rel.ToColumn}]");
                        joinedTables.Add(rel.FromTable);
                    }
                    // else: both tables already joined, skip the JOIN but the ON condition
                    // is implicitly satisfied through the existing join chain
                }
            }

            // WHERE clause (from filters) - Quote column names
            if (model.Filters != null && model.Filters.Any())
            {
                sql.AppendLine("WHERE");
                var conditions = model.Filters.Select((f, index) =>
                {
                    var condition = new System.Text.StringBuilder();

                    // Add logical operator for non-first filters
                    if (index > 0)
                    {
                        condition.Append($"    {f.LogicalOperator ?? "AND"} ");
                    }
                    else
                    {
                        condition.Append("    ");
                    }

                    // Build the condition based on operator
                    if (f.Operator.Equals("IS NULL", StringComparison.OrdinalIgnoreCase) ||
                        f.Operator.Equals("IS NOT NULL", StringComparison.OrdinalIgnoreCase))
                    {
                        // IS NULL and IS NOT NULL don't need a value
                        condition.Append($"{f.TableAlias}.[{f.ColumnName}] {f.Operator}");
                    }
                    else if (f.Operator.Equals("IN", StringComparison.OrdinalIgnoreCase))
                    {
                        // IN operator - wrap each value in quotes for string columns
                        var inValues = f.Value.Split(',')
                            .Select(v => $"'{v.Trim()}'")
                            .ToArray();
                        condition.Append($"{f.TableAlias}.[{f.ColumnName}] {f.Operator} ({string.Join(", ", inValues)})");
                    }
                    else if (f.Operator.Equals("LIKE", StringComparison.OrdinalIgnoreCase))
                    {
                        // LIKE operator - wrap value with wildcards if not already present
                        var likeValue = f.Value;
                        if (!likeValue.Contains("%"))
                        {
                            likeValue = $"%{likeValue}%";
                        }
                        condition.Append($"{f.TableAlias}.[{f.ColumnName}] {f.Operator} '{likeValue}'");
                    }
                    else
                    {
                        // Standard operators (=, !=, >, <, >=, <=)
                        condition.Append($"{f.TableAlias}.[{f.ColumnName}] {f.Operator} '{f.Value}'");
                    }

                    return condition.ToString();
                });

                sql.AppendLine(string.Join("\n", conditions));
            }

            return sql.ToString();
        }

        /// <summary>
        /// Delete a report
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();

            var canDelete = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_DELETE");
            if (!canDelete)
            {
                return Json(new { success = false, message = "You don't have permission to delete reports" });
            }

            var report = await _context.ReportDefinitions
                .FirstOrDefaultAsync(r => r.ReportDefinitionId == id);

            if (report == null)
            {
                return Json(new { success = false, message = "Report not found" });
            }

            // Soft delete
            report.IsActive = false;
            report.ModifiedDate = DateTime.UtcNow;
            report.ModifiedByUserId = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted report {ReportId}", userId, id);

            return Json(new { success = true, message = "Report deleted successfully" });
        }

        /// <summary>
        /// Detects parameters in SQL query (looks for @ParameterName patterns)
        /// </summary>
        private List<ReportParameter> DetectParametersFromSql(string sql)
        {
            var parameters = new List<ReportParameter>();

            // Regex to find @ParameterName patterns
            var parameterPattern = new System.Text.RegularExpressions.Regex(@"@(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = parameterPattern.Matches(sql);

            var uniqueParams = new HashSet<string>();

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var paramName = match.Groups[1].Value;

                // Skip if already added
                if (uniqueParams.Contains(paramName))
                    continue;

                uniqueParams.Add(paramName);

                // Check if this parameter is used inside an IN() clause
                var inPattern = $@"IN\s*\(\s*@{System.Text.RegularExpressions.Regex.Escape(paramName)}\s*\)";
                var isInClause = System.Text.RegularExpressions.Regex.IsMatch(sql, inPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var displayName = FormatParameterDisplayName(paramName);

                // Create parameter with default settings
                var parameter = new ReportParameter
                {
                    Name = paramName,
                    DisplayName = displayName,
                    DataType = GuessParameterDataType(paramName),
                    IsRequired = true,
                    PromptText = isInClause
                        ? $"Enter {displayName} (comma-separated for multiple values, e.g. VALUE1, VALUE2)"
                        : $"Enter {displayName}"
                };

                parameters.Add(parameter);
            }

            return parameters;
        }

        /// <summary>
        /// Formats parameter name for display (e.g., "WarehouseID" -> "Warehouse ID")
        /// </summary>
        private string FormatParameterDisplayName(string paramName)
        {
            // Insert spaces before capital letters
            var result = System.Text.RegularExpressions.Regex.Replace(paramName, "([A-Z])", " $1").Trim();

            // Capitalize first letter
            if (result.Length > 0)
            {
                result = char.ToUpper(result[0]) + result.Substring(1);
            }

            return result;
        }

        /// <summary>
        /// Guesses parameter data type based on parameter name
        /// </summary>
        private string GuessParameterDataType(string paramName)
        {
            var lowerName = paramName.ToLower();

            if (lowerName.Contains("date") || lowerName.Contains("from") || lowerName.Contains("to") ||
                lowerName.Contains("start") || lowerName.Contains("end"))
            {
                return "date";
            }

            if (lowerName.Contains("id") || lowerName.Contains("number") || lowerName.Contains("count") ||
                lowerName.Contains("amount") || lowerName.Contains("qty") || lowerName.Contains("quantity"))
            {
                return "number";
            }

            return "text";
        }

        /// <summary>
        /// Validates SQL query for security (ensures SELECT only, no DDL/DML)
        /// </summary>
        private (bool isValid, string? errorMessage) ValidateCustomSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return (false, "SQL query cannot be empty");
            }

            // Remove comments and normalize whitespace
            var normalized = System.Text.RegularExpressions.Regex.Replace(sql, @"--.*?$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            normalized = normalized.Trim().ToUpper();

            // Must start with SELECT
            if (!normalized.StartsWith("SELECT"))
            {
                return (false, "Query must be a SELECT statement");
            }

            // Blocked keywords (DDL/DML operations)
            var blockedKeywords = new[]
            {
                "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
                "TRUNCATE", "EXEC", "EXECUTE", "SP_", "XP_", "BACKUP",
                "RESTORE", "GRANT", "REVOKE", "DENY"
            };

            foreach (var keyword in blockedKeywords)
            {
                if (normalized.Contains(keyword))
                {
                    return (false, $"Query contains blocked keyword: {keyword}");
                }
            }

            // Check for semicolons (multiple statements)
            if (sql.Trim().Count(c => c == ';') > 1)
            {
                return (false, "Multiple SQL statements are not allowed");
            }

            return (true, null);
        }

        /// <summary>
        /// Execute report (GET) - Shows parameter form if report has parameters
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Execute(int id)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Check if user has access
                var hasAccess = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ACCESS");
                if (!hasAccess)
                {
                    _logger.LogWarning("User {UserId} attempted to execute report without permission", userId);
                    return Forbid();
                }

                // Load report definition
                var report = await _context.ReportDefinitions
                    .FirstOrDefaultAsync(r => r.ReportDefinitionId == id && r.IsActive);

                if (report == null)
                {
                    _logger.LogWarning("Report {ReportId} not found or inactive", id);
                    return NotFound();
                }

                _logger.LogInformation("User {UserId} loading report {ReportId}: {ReportName}", userId, id, report.Name);

                // Check if report has parameters
                var parameters = new List<ReportParameter>();
                if (!string.IsNullOrEmpty(report.QueryDefinitionJson))
                {
                    try
                    {
                        using var document = System.Text.Json.JsonDocument.Parse(report.QueryDefinitionJson);
                        var root = document.RootElement;

                        if (root.TryGetProperty("parameters", out var paramsElement))
                        {
                            var jsonOptions = new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            parameters = System.Text.Json.JsonSerializer.Deserialize<List<ReportParameter>>(paramsElement.GetRawText(), jsonOptions) ?? new List<ReportParameter>();
                            _logger.LogInformation("Report has {ParamCount} parameters", parameters.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not parse parameters from QueryDefinitionJson");
                    }
                }

                // If report has parameters, show parameter entry form
                if (parameters.Any())
                {
                    var viewModel = new ReportExecutionViewModel
                    {
                        ReportId = report.ReportDefinitionId,
                        ReportName = report.Name ?? "Untitled Report",
                        ReportDescription = report.Description ?? "",
                        Category = report.Category ?? "Custom Reports",
                        GeneratedSql = report.GeneratedSql ?? "",
                        Parameters = parameters,
                        QueryDefinitionJson = report.QueryDefinitionJson,
                        ColumnNames = new List<string>(),
                        ResultData = new List<Dictionary<string, object>>(),
                        RowCount = 0,
                        ExecutionTimeMs = 0,
                        ExecutedDate = DateTime.Now
                    };

                    return View(viewModel);
                }

                // No parameters - execute directly
                return await ExecuteReportWithParameters(report, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading report {ReportId}", id);
                TempData["ErrorMessage"] = $"Error loading report: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Execute report (POST) - Executes report with provided parameter values
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Execute(int id, [FromForm] Dictionary<string, string> parameterValues)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Check if user has access
                var hasAccess = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ACCESS");
                if (!hasAccess)
                {
                    _logger.LogWarning("User {UserId} attempted to execute report without permission", userId);
                    return Forbid();
                }

                // Load report definition
                var report = await _context.ReportDefinitions
                    .FirstOrDefaultAsync(r => r.ReportDefinitionId == id && r.IsActive);

                if (report == null)
                {
                    _logger.LogWarning("Report {ReportId} not found or inactive", id);
                    return NotFound();
                }

                _logger.LogInformation("User {UserId} executing report {ReportId}: {ReportName} with parameters", userId, id, report.Name);

                // Execute with parameters
                return await ExecuteReportWithParameters(report, parameterValues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing report {ReportId}", id);
                TempData["ErrorMessage"] = $"Error executing report: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Helper method to execute report with optional parameter values
        /// </summary>
        private async Task<IActionResult> ExecuteReportWithParameters(Data.Entities.QueryBuilder.ReportDefinitionEntity report, Dictionary<string, string>? parameterValues)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new System.Data.SqlClient.SqlConnection(connStr);
            await conn.OpenAsync();

            // Check if this is a SQL-mode report (no fully-qualified table names)
            // and switch to the user's CompUFloor database so table names resolve
            var isSqlMode = report.QueryDefinitionJson?.Contains("\"queryMode\":\"sql\"", StringComparison.OrdinalIgnoreCase) == true
                         || report.QueryDefinitionJson?.Contains("\"queryMode\": \"sql\"", StringComparison.OrdinalIgnoreCase) == true;
            if (isSqlMode)
            {
                var userLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
                var compuFloorDb = GetCompUFloorDatabaseName(userLocation);
                _logger.LogInformation("SQL-mode report: switching to database {Database}", compuFloorDb);
                await conn.ChangeDatabaseAsync(compuFloorDb);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var sqlToExecute = report.GeneratedSql;
            var cmd = new System.Data.SqlClient.SqlCommand();
            cmd.Connection = conn;
            cmd.CommandTimeout = 60; // 60 seconds timeout for report execution

            // Add parameters if provided
            if (parameterValues != null && parameterValues.Any())
            {
                foreach (var param in parameterValues)
                {
                    var paramName = param.Key;
                    var paramValue = param.Value;

                    // Ensure parameter name starts with @
                    if (!paramName.StartsWith("@"))
                    {
                        paramName = "@" + paramName;
                    }

                    _logger.LogInformation("Adding parameter {ParamName} = {ParamValue}", paramName, paramValue);

                    // Check if this parameter is used inside an IN() clause and has comma-separated values
                    // Pattern: IN ( @ParamName ) with optional whitespace
                    var inPattern = $@"IN\s*\(\s*{System.Text.RegularExpressions.Regex.Escape(paramName)}\s*\)";
                    if (!string.IsNullOrEmpty(paramValue)
                        && paramValue.Contains(',')
                        && System.Text.RegularExpressions.Regex.IsMatch(sqlToExecute, inPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        // Expand comma-separated value into individual parameters
                        // e.g. @ProductClass with "CARPET,VINYL" becomes @ProductClass_0, @ProductClass_1
                        var values = paramValue.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToArray();
                        var expandedParams = new List<string>();
                        for (int i = 0; i < values.Length; i++)
                        {
                            var expandedName = $"{paramName}_{i}";
                            expandedParams.Add(expandedName);
                            cmd.Parameters.AddWithValue(expandedName, values[i]);
                        }
                        // Replace IN(@ParamName) with IN(@ParamName_0, @ParamName_1, ...)
                        sqlToExecute = System.Text.RegularExpressions.Regex.Replace(
                            sqlToExecute,
                            inPattern,
                            $"IN ({string.Join(", ", expandedParams)})",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        _logger.LogInformation("Expanded IN clause for {ParamName}: {Count} values", paramName, values.Length);
                    }
                    else
                    {
                        // Standard single-value parameter
                        cmd.Parameters.AddWithValue(paramName, string.IsNullOrEmpty(paramValue) ? DBNull.Value : paramValue);
                    }
                }
            }

            cmd.CommandText = sqlToExecute;

            var resultData = new List<Dictionary<string, object>>();
            var columnNames = new List<string>();
            var columnTypes = new Dictionary<string, string>();

            using var reader = await cmd.ExecuteReaderAsync();

            // Get column names and types
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i);
                columnNames.Add(colName);
                columnTypes[colName] = reader.GetFieldType(i).Name;
            }

            // Read all rows
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                resultData.Add(row);
            }

            stopwatch.Stop();

            _logger.LogInformation("Report executed successfully. Rows: {RowCount}, Time: {Time}ms", resultData.Count, stopwatch.ElapsedMilliseconds);

            // Build view model
            var viewModel = new ReportExecutionViewModel
            {
                ReportId = report.ReportDefinitionId,
                ReportName = report.Name ?? "Untitled Report",
                ReportDescription = report.Description ?? "",
                Category = report.Category ?? "Custom Reports",
                GeneratedSql = report.GeneratedSql ?? "",
                ColumnNames = columnNames,
                ResultData = resultData,
                RowCount = resultData.Count,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ExecutedDate = DateTime.Now,
                QueryDefinitionJson = report.QueryDefinitionJson,
                Parameters = new List<ReportParameter>()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Helper method to get current user ID from claims
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("Users_ID")?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
