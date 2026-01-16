using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using SalesMetrics.Models;
using SalesMetrics.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SalesMetrics.Controllers
{
    public class GMRecapController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IErrorLoggingService _errorLoggingService;
        private readonly IMemoryCache _cache;
        private const string RECAP_LIST_CACHE_KEY = "GMRecapList";
        private const int CACHE_DURATION_MINUTES = 5;

        // RESTORED: Constructor with IErrorLoggingService dependency injection
        public GMRecapController(IConfiguration configuration, IErrorLoggingService errorLoggingService, IMemoryCache cache)
        {
            _configuration = configuration;
            _errorLoggingService = errorLoggingService;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> RecapEntry()
        {
            try
            {
                // RESTORED: Proper session validation
                var userClaim = HttpContext.User.FindFirst("Users_ID");
                if (userClaim == null || !int.TryParse(userClaim.Value, out int user_id))
                {
                    TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                    return RedirectToAction("Login", "Auth");
                }

                var location = HttpContext.User.FindFirst("LocationId")?.Value;
                int locationId = location != null ? int.Parse(location) : 0;

                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

                var model = new GMRecapCardViewModel
                {
                    WeekStartDate = weekStart,
                    GMUserID = user_id,
                    LocationID = locationId,
                    Fields = new List<RecapField>()
                };

                // RESTORED: Load existing recap data for visual feedback (badges/progress)
                await LoadExistingRecapData(model);

                return View("RecapEntry", model);
            }
            catch (Exception ex)
            {
                await _errorLoggingService.LogErrorAsync(ex, HttpContext, null, "GMRecap-RecapEntry");
                TempData["ErrorMessage"] = "An error occurred loading the recap entry form.";
                return RedirectToAction("Index", "Home");
            }
        }

        // Complete LoadExistingRecapData method - replaces your existing one
        private async Task LoadExistingRecapData(GMRecapCardViewModel model)
        {
            try
            {
                // Get user info from claims since the model might not have it yet
                var userClaim = HttpContext.User.FindFirst("Users_ID");
                var locationClaim = HttpContext.User.FindFirst("LocationId");

                if (userClaim != null && int.TryParse(userClaim.Value, out int userId))
                {
                    model.GMUserID = userId;
                }

                if (locationClaim != null && int.TryParse(locationClaim.Value, out int locationId))
                {
                    model.LocationID = locationId;
                }

                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Check for existing recap entry (including drafts)
                var checkRecapCmd = new SqlCommand(@"
                    SELECT RecapID FROM GMWeeklyRecapEntry
                    WHERE GMUserID = @GMUserID AND LocationID = @LocationID AND WeekStartDate = @WeekStartDate
                ", conn);

                checkRecapCmd.Parameters.AddWithValue("@GMUserID", model.GMUserID);
                checkRecapCmd.Parameters.AddWithValue("@LocationID", model.LocationID);
                checkRecapCmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

                var recapId = await checkRecapCmd.ExecuteScalarAsync();
                if (recapId != null)
                {
                    model.RecapID = (int)recapId;

                    // Load existing fields
                    var cmd = new SqlCommand(@"
                SELECT FieldName, FieldValue, CreatedDate
                FROM GMWeeklyRecapField 
                WHERE RecapID = @RecapID
            ", conn);
                    cmd.Parameters.AddWithValue("@RecapID", model.RecapID);

                    model.Fields = new List<RecapField>();
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        model.Fields.Add(new RecapField
                        {
                            FieldName = reader["FieldName"].ToString(),
                            FieldValue = reader["FieldValue"].ToString(),
                            CreatedDate = reader.IsDBNull("CreatedDate") ? DateTime.Now : reader.GetDateTime("CreatedDate")
                        });
                    }
                }
                else
                {
                    // Initialize empty fields list if no existing recap
                    model.Fields = new List<RecapField>();
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the page load for existing data retrieval issues
                await _errorLoggingService.LogWarningAsync(
                    "Failed to load existing recap data",
                    HttpContext,
                    JsonSerializer.Serialize(new { Model = model, Error = ex.Message }),
                    "GMRecap-LoadData"
                );

                // Ensure Fields is initialized even if there's an error
                model.Fields ??= new List<RecapField>();
            }
        }

        // Complete SubmitWeeklyRecap method - replaces your existing one
        [HttpPost]
        public async Task<IActionResult> SubmitWeeklyRecap(GMRecapCardViewModel model)
        {
            // Validation - check if at least one field has content
            bool hasValidFields = model.Fields != null && model.Fields.Any(f => !string.IsNullOrWhiteSpace(f.FieldValue));
            if (!hasValidFields)
            {
                var validationErrors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                await _errorLoggingService.LogWarningAsync(
                    $"Model validation failed: {string.Join(", ", validationErrors)}",
                    HttpContext,
                    JsonSerializer.Serialize(new { ModelState, Model = model }),
                    "GMRecap"
                );

                TempData["ErrorMessage"] = "Please enter at least one recap field before submitting.";
                return View("RecapEntry", model);
            }

            var userClaim = HttpContext.User.FindFirst("Users_ID");
            if (userClaim == null || !int.TryParse(userClaim.Value, out int user_id))
            {
                TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                return RedirectToAction("Login", "Auth");
            }

            var location = HttpContext.User.FindFirst("LocationId")?.Value;
            model.GMUserID = user_id;
            model.LocationID = location != null ? int.Parse(location) : 0;

            var connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                int entryId;

                // Check if entry already exists for the week (including drafts)
                var checkCmd = new SqlCommand(@"
            SELECT RecapID FROM GMWeeklyRecapEntry
            WHERE GMUserID = @GMUserID AND LocationID = @LocationID AND WeekStartDate = @WeekStartDate
        ", conn, tran);
                checkCmd.Parameters.AddWithValue("@GMUserID", user_id);
                checkCmd.Parameters.AddWithValue("@LocationID", model.LocationID);
                checkCmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

                var existing = await checkCmd.ExecuteScalarAsync();
                if (existing != null)
                {
                    entryId = (int)existing;

                    // Convert draft to final submission and update modified date
                    var updateEntryCmd = new SqlCommand(@"
                UPDATE GMWeeklyRecapEntry 
                SET IsDraft = 0, ModifiedDate = GETDATE(), SubmittedDate = GETDATE()
                WHERE RecapID = @RecapID
            ", conn, tran);
                    updateEntryCmd.Parameters.AddWithValue("@RecapID", entryId);
                    await updateEntryCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // Create new final entry
                    var insertEntryCmd = new SqlCommand(@"
                INSERT INTO GMWeeklyRecapEntry (GMUserID, LocationID, WeekStartDate, CreatedDate, IsDraft, SubmittedDate)
                OUTPUT INSERTED.RecapID
                VALUES (@GMUserID, @LocationID, @WeekStartDate, GETDATE(), 0, GETDATE())
            ", conn, tran);
                    insertEntryCmd.Parameters.AddWithValue("@GMUserID", user_id);
                    insertEntryCmd.Parameters.AddWithValue("@LocationID", model.LocationID);
                    insertEntryCmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

                    entryId = (int)await insertEntryCmd.ExecuteScalarAsync();
                }

                // Process each field in the submission
                // CHANGED: Always insert new instances instead of updating existing ones
                foreach (var field in model.Fields.Where(f => !string.IsNullOrWhiteSpace(f.FieldValue)))
                {
                    // Always insert a new field entry - allows multiple submissions for the same field
                    var insertFieldCmd = new SqlCommand(@"
                        INSERT INTO GMWeeklyRecapField (RecapID, FieldName, FieldValue, CreatedDate)
                        VALUES (@RecapID, @FieldName, @FieldValue, GETDATE())
                    ", conn, tran);
                    insertFieldCmd.Parameters.AddWithValue("@RecapID", entryId);
                    insertFieldCmd.Parameters.AddWithValue("@FieldName", field.FieldName);
                    insertFieldCmd.Parameters.AddWithValue("@FieldValue", field.FieldValue);
                    await insertFieldCmd.ExecuteNonQueryAsync();
                }

                await tran.CommitAsync();

                // Invalidate cache after successful submission
                _cache.Remove(RECAP_LIST_CACHE_KEY);

                // Provide feedback to user
                TempData["RecapSubmitted"] = "true";

                return RedirectToAction("RecapEntry");
            }
            catch (SqlException sqlEx)
            {
                await tran.RollbackAsync();

                // Log SQL-specific errors with additional context
                var additionalData = JsonSerializer.Serialize(new
                {
                    ModelData = model,
                    UserId = user_id,
                    Location = model.LocationID,
                    SqlErrorNumber = sqlEx.Number,
                    SqlErrorSeverity = sqlEx.Class,
                    SqlErrorState = sqlEx.State,
                    SqlErrorProcedure = sqlEx.Procedure,
                    SqlErrorLineNumber = sqlEx.LineNumber
                });

                await _errorLoggingService.LogErrorAsync(sqlEx, HttpContext, additionalData, "GMRecap-Database");
                TempData["ErrorMessage"] = "A database error occurred while saving your recap. Please try again.";
                return View("RecapEntry", model);
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();

                // Log general errors with additional context
                var additionalData = JsonSerializer.Serialize(new
                {
                    ModelData = model,
                    UserId = user_id,
                    Location = model.LocationID,
                    Action = "SubmitRecap",
                    TimeStamp = DateTime.Now
                });

                await _errorLoggingService.LogErrorAsync(ex, HttpContext, additionalData, "GMRecap");
                TempData["ErrorMessage"] = "An error occurred while saving your recap. Please try again or contact support.";
                return View("RecapEntry", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> RecapList()
        {
            // Try to get cached data first
            if (_cache.TryGetValue(RECAP_LIST_CACHE_KEY, out List<GMRecapCardViewModel> cachedRecaps))
            {
                return View("RecapList", cachedRecaps);
            }

            var connStr = _configuration.GetConnectionString("SalesMetrics");
            var recapCards = new List<GMRecapCardViewModel>();

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // OPTIMIZED: Get all recaps with GM names
            var recapCmd = new SqlCommand(@"
        SELECT E.RecapID, E.WeekStartDate, E.GMUserID, E.LocationID,
               U.FirstName + ' ' + U.LastName as GMName, E.CreatedDate
        FROM [dbo].[GMWeeklyRecapEntry] E
        LEFT JOIN [dbo].[Users] U ON E.GMUserID = U.Users_ID
        ORDER BY E.WeekStartDate DESC
    ", conn);

            using var reader = await recapCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                recapCards.Add(new GMRecapCardViewModel
                {
                    RecapID = reader.GetInt32(0),
                    WeekStartDate = reader.GetDateTime(1),
                    GMUserID = reader.GetInt32(2),
                    LocationID = reader.GetInt32(3),
                    GMName = reader.IsDBNull(4) ? "Unknown GM" : reader.GetString(4),
                    CreatedDate = reader.GetDateTime(5),
                    Fields = new List<RecapField>()
                });
            }
            reader.Close();

            // OPTIMIZED: Load ALL fields in ONE query (fixes N+1 problem)
            if (recapCards.Any())
            {
                var recapIds = string.Join(",", recapCards.Select(r => r.RecapID));
                var fieldsCmd = new SqlCommand($@"
            SELECT RecapID, FieldName, FieldValue, CreatedDate
            FROM [dbo].[GMWeeklyRecapField]
            WHERE RecapID IN ({recapIds})
            ORDER BY RecapID
        ", conn);

                using var fieldReader = await fieldsCmd.ExecuteReaderAsync();

                // Group fields by RecapID for quick lookup
                var fieldsByRecapId = new Dictionary<int, List<RecapField>>();

                while (await fieldReader.ReadAsync())
                {
                    int recapId = fieldReader.GetInt32(0);
                    var field = new RecapField
                    {
                        FieldName = fieldReader.GetString(1),
                        FieldValue = fieldReader.IsDBNull(2) ? "" : fieldReader.GetString(2),
                        CreatedDate = fieldReader.IsDBNull(3) ? DateTime.MinValue : fieldReader.GetDateTime(3)
                    };

                    if (!fieldsByRecapId.ContainsKey(recapId))
                    {
                        fieldsByRecapId[recapId] = new List<RecapField>();
                    }
                    fieldsByRecapId[recapId].Add(field);
                }

                // Assign fields to their respective recaps
                foreach (var recap in recapCards)
                {
                    if (fieldsByRecapId.TryGetValue(recap.RecapID, out var fields))
                    {
                        recap.Fields = fields;
                    }
                }
            }

            // Cache the results for 5 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            _cache.Set(RECAP_LIST_CACHE_KEY, recapCards, cacheOptions);

            return View("RecapList", recapCards);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecapDetails(int recapId)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var entry = new GMRecapCardViewModel();

            // Get recap entry with GM name - FIXED SQL SYNTAX
            var cmd = new SqlCommand(@"
        SELECT E.RecapID, E.WeekStartDate, E.GMUserID, E.LocationID, E.CreatedDate,
               U.FirstName + ' ' + U.LastName as GMName
        FROM [dbo].[GMWeeklyRecapEntry] E
        LEFT JOIN [dbo].[Users] U ON E.GMUserID = U.Users_ID
        WHERE E.RecapID = @RecapID
    ", conn);
            cmd.Parameters.AddWithValue("@RecapID", recapId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                entry.RecapID = reader.GetInt32(0);
                entry.WeekStartDate = reader.GetDateTime(1);
                entry.GMUserID = reader.GetInt32(2);
                entry.LocationID = reader.GetInt32(3);
                entry.CreatedDate = reader.GetDateTime(4);
                entry.GMName = reader.IsDBNull(5) ? "Unknown GM" : reader.GetString(5);
            }
            reader.Close();

            // Get fields
            var fieldCmd = new SqlCommand(@"
        SELECT FieldName, FieldValue, CreatedDate
        FROM [dbo].[GMWeeklyRecapField] 
        WHERE RecapID = @RecapID
    ", conn);
            fieldCmd.Parameters.AddWithValue("@RecapID", recapId);

            entry.Fields = new List<RecapField>();
            using var fieldReader = await fieldCmd.ExecuteReaderAsync();
            while (await fieldReader.ReadAsync())
            {
                entry.Fields.Add(new RecapField
                {
                    FieldName = fieldReader.GetString(0),
                    FieldValue = fieldReader.IsDBNull(1) ? "" : fieldReader.GetString(1),
                    CreatedDate = fieldReader.IsDBNull(2) ? DateTime.MinValue : fieldReader.GetDateTime(2)
                });
            }

            return PartialView("_RecapDetailsPartial", entry);
        }

        [HttpGet]
        public async Task<IActionResult> EditRecapModal(int recapId)
        {
            // FIX: Move declaration OUTSIDE try block
            var currentUserClaim = HttpContext.User.FindFirst("Users_ID");

            try
            {
                if (currentUserClaim == null || !int.TryParse(currentUserClaim.Value, out int currentUserId))
                {
                    return Json(new { success = false, message = "Authentication required" });
                }

                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var recap = new GMRecapCardViewModel();

                // Get the recap and verify ownership
                var cmd = new SqlCommand(@"
            SELECT RecapID, WeekStartDate, GMUserID, LocationID, CreatedDate 
            FROM GMWeeklyRecapEntry 
            WHERE RecapID = @RecapID", conn);
                cmd.Parameters.AddWithValue("@RecapID", recapId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    recap.RecapID = reader.GetInt32(0);
                    recap.WeekStartDate = reader.GetDateTime(1);
                    recap.GMUserID = reader.GetInt32(2);
                    recap.LocationID = reader.GetInt32(3);
                    recap.CreatedDate = reader.GetDateTime(4);

                    // Security check: Only allow the creator to edit
                    if (recap.GMUserID != currentUserId)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Access denied. You can only edit your own recap entries."
                        });
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Recap entry not found" });
                }
                reader.Close();

                // Fetch fields
                recap.Fields = new List<RecapField>();
                var fieldCmd = new SqlCommand(@"
            SELECT FieldName, FieldValue 
            FROM GMWeeklyRecapField 
            WHERE RecapID = @RecapID", conn);
                fieldCmd.Parameters.AddWithValue("@RecapID", recapId);

                using var fieldReader = await fieldCmd.ExecuteReaderAsync();
                while (await fieldReader.ReadAsync())
                {
                    recap.Fields.Add(new RecapField
                    {
                        FieldName = fieldReader.GetString(0),
                        FieldValue = fieldReader.IsDBNull(1) ? "" : fieldReader.GetString(1)
                    });
                }

                return PartialView("_EditRecapModal", recap);
            }
            catch (Exception ex)
            {
                await _errorLoggingService.LogErrorAsync(ex, HttpContext,
                    JsonSerializer.Serialize(new { RecapId = recapId, UserId = currentUserClaim?.Value }),
                    "GMRecap-EditRecapModal");

                return Json(new { success = false, message = "An error occurred while loading the edit form." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRecap(GMRecapCardViewModel model)
        {
            // FIX: Move declaration OUTSIDE try block
            var currentUserClaim = HttpContext.User.FindFirst("Users_ID");

            try
            {
                if (currentUserClaim == null || !int.TryParse(currentUserClaim.Value, out int currentUserId))
                {
                    TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                    return RedirectToAction("Login", "Auth");
                }

                if (model.RecapID <= 0 || model.Fields == null || !model.Fields.Any())
                {
                    TempData["ErrorMessage"] = "Invalid recap data submitted.";
                    return RedirectToAction("RecapList");
                }

                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Security check: Verify the current user owns this recap
                var ownerCheckCmd = new SqlCommand(@"
            SELECT GMUserID FROM GMWeeklyRecapEntry WHERE RecapID = @RecapID", conn);
                ownerCheckCmd.Parameters.AddWithValue("@RecapID", model.RecapID);

                var ownerResult = await ownerCheckCmd.ExecuteScalarAsync();
                if (ownerResult == null || (int)ownerResult != currentUserId)
                {
                    TempData["ErrorMessage"] = "Access denied. You can only edit your own recap entries.";
                    return RedirectToAction("RecapList");
                }

                using var tran = conn.BeginTransaction();

                try
                {
                    // Update the main recap entry
                    var updateRecapEntry = new SqlCommand(@"
                UPDATE GMWeeklyRecapEntry 
                SET ModifiedDate = GETDATE(),
                    ModifiedBy = @ModifiedBy
                WHERE RecapID = @RecapID AND GMUserID = @GMUserID", conn, tran);
                    updateRecapEntry.Parameters.AddWithValue("@RecapID", model.RecapID);
                    updateRecapEntry.Parameters.AddWithValue("@GMUserID", currentUserId); // Double-check ownership
                    updateRecapEntry.Parameters.AddWithValue("@ModifiedBy", currentUserClaim.Value ?? "Unknown");
                    await updateRecapEntry.ExecuteNonQueryAsync();

                    foreach (var field in model.Fields)
                    {
                        var updateFieldCmd = new SqlCommand(@"
                    UPDATE GMWeeklyRecapField 
                    SET FieldValue = @FieldValue, ModifiedDate = GETDATE()
                    WHERE RecapID = @RecapID AND FieldName = @FieldName", conn, tran);
                        updateFieldCmd.Parameters.AddWithValue("@RecapID", model.RecapID);
                        updateFieldCmd.Parameters.AddWithValue("@FieldName", field.FieldName);
                        updateFieldCmd.Parameters.AddWithValue("@FieldValue", field.FieldValue ?? "");

                        await updateFieldCmd.ExecuteNonQueryAsync();
                    }

                    await tran.CommitAsync();

                    // Invalidate cache after successful update
                    _cache.Remove(RECAP_LIST_CACHE_KEY);

                    TempData["SuccessMessage"] = "Your recap has been updated successfully.";
                    return RedirectToAction("RecapList");
                }
                catch (Exception ex)
                {
                    await tran.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                await _errorLoggingService.LogErrorAsync(ex, HttpContext,
                    JsonSerializer.Serialize(new { Model = model, UserId = currentUserClaim?.Value }),
                    "GMRecap-Update");

                TempData["ErrorMessage"] = "An error occurred while updating the recap.";
                return RedirectToAction("RecapList");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRecap(int recapId)
        {
            try
            {
                // Get current user ID
                var currentUserClaim = HttpContext.User.FindFirst("Users_ID");
                if (currentUserClaim == null || !int.TryParse(currentUserClaim.Value, out int currentUserId))
                {
                    TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                    return RedirectToAction("Login", "Auth");
                }

                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Security check: Verify the current user owns this recap
                var ownerCheckCmd = new SqlCommand(@"
            SELECT GMUserID FROM GMWeeklyRecapEntry WHERE RecapID = @RecapID", conn);
                ownerCheckCmd.Parameters.AddWithValue("@RecapID", recapId);

                var ownerResult = await ownerCheckCmd.ExecuteScalarAsync();
                if (ownerResult == null || (int)ownerResult != currentUserId)
                {
                    TempData["ErrorMessage"] = "Access denied. You can only delete your own recap entries.";
                    return RedirectToAction("RecapList");
                }

                using var tran = conn.BeginTransaction();
                try
                {
                    // Step 1: Delete fields first
                    var deleteFieldsCmd = new SqlCommand(
                        "DELETE FROM GMWeeklyRecapField WHERE RecapID = @RecapID", conn, tran);
                    deleteFieldsCmd.Parameters.AddWithValue("@RecapID", recapId);
                    await deleteFieldsCmd.ExecuteNonQueryAsync();

                    // Step 2: Delete the entry (with ownership check)
                    var deleteEntryCmd = new SqlCommand(
                        "DELETE FROM GMWeeklyRecapEntry WHERE RecapID = @RecapID AND GMUserID = @GMUserID", conn, tran);
                    deleteEntryCmd.Parameters.AddWithValue("@RecapID", recapId);
                    deleteEntryCmd.Parameters.AddWithValue("@GMUserID", currentUserId);

                    var deletedRows = await deleteEntryCmd.ExecuteNonQueryAsync();
                    if (deletedRows == 0)
                    {
                        await tran.RollbackAsync();
                        TempData["ErrorMessage"] = "Access denied or entry not found.";
                        return RedirectToAction("RecapList");
                    }

                    await tran.CommitAsync();

                    // Invalidate cache after successful deletion
                    _cache.Remove(RECAP_LIST_CACHE_KEY);

                    TempData["SuccessMessage"] = "Your recap entry has been deleted successfully.";
                }
                catch (SqlException sqlEx)
                {
                    await tran.RollbackAsync();

                    var additionalData = JsonSerializer.Serialize(new
                    {
                        RecapId = recapId,
                        UserId = currentUserId,
                        SqlErrorNumber = sqlEx.Number,
                        SqlErrorSeverity = sqlEx.Class,
                        SqlErrorState = sqlEx.State,
                        SqlErrorProcedure = sqlEx.Procedure,
                        SqlErrorLineNumber = sqlEx.LineNumber
                    });

                    await _errorLoggingService.LogErrorAsync(sqlEx, HttpContext, additionalData, "GMRecap-DeleteEntry");
                    TempData["ErrorMessage"] = "A database error occurred while deleting the recap entry.";
                }
                catch (Exception ex)
                {
                    await tran.RollbackAsync();

                    await _errorLoggingService.LogErrorAsync(ex, HttpContext,
                        JsonSerializer.Serialize(new { RecapId = recapId, UserId = currentUserId }),
                        "GMRecap-DeleteEntry");

                    TempData["ErrorMessage"] = "An error occurred while deleting the recap entry.";
                }

                return RedirectToAction("RecapList");
            }
            catch (Exception ex)
            {
                await _errorLoggingService.LogErrorAsync(ex, HttpContext,
                    JsonSerializer.Serialize(new { RecapId = recapId }),
                    "GMRecap-DeleteEntry");

                TempData["ErrorMessage"] = "An error occurred while processing your request.";
                return RedirectToAction("RecapList");
            }
        }

        // Add the SaveDraft method for auto-save functionality
        [HttpPost]
        public async Task<IActionResult> SaveDraft([FromBody] GMRecapCardViewModel model)
        {
            try
            {
                // CRITICAL: Check if model is null (deserialization failed)
                if (model == null)
                {
                    await _errorLoggingService.LogWarningAsync(
                        "SaveDraft received null model - JSON deserialization failed",
                        HttpContext,
                        JsonSerializer.Serialize(new { ContentType = HttpContext.Request.ContentType }),
                        "GMRecap-SaveDraft"
                    );
                    return Json(new { success = false, message = "Invalid data format - model is null" });
                }

                // Additional validation
                if (model.WeekStartDate == default)
                {
                    return Json(new { success = false, message = "Invalid week start date" });
                }

                var userClaim = HttpContext.User.FindFirst("Users_ID");
                if (userClaim == null || !int.TryParse(userClaim.Value, out int user_id))
                {
                    return Json(new { success = false, message = "Session expired" });
                }

                var location = HttpContext.User.FindFirst("LocationId")?.Value;
                model.GMUserID = user_id;
                model.LocationID = location != null ? int.Parse(location) : 0;

                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                using var tran = conn.BeginTransaction();

                try
                {
                    // Check if draft entry exists
                    var checkCmd = new SqlCommand(@"
                SELECT RecapID FROM GMWeeklyRecapEntry
                WHERE GMUserID = @GMUserID AND LocationID = @LocationID 
                AND WeekStartDate = @WeekStartDate AND IsDraft = 1
            ", conn, tran);

                    checkCmd.Parameters.AddWithValue("@GMUserID", user_id);
                    checkCmd.Parameters.AddWithValue("@LocationID", model.LocationID);
                    checkCmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

                    var existing = await checkCmd.ExecuteScalarAsync();
                    int entryId;

                    if (existing != null)
                    {
                        entryId = (int)existing;

                        // Update last modified
                        var updateCmd = new SqlCommand(@"
                    UPDATE GMWeeklyRecapEntry 
                    SET ModifiedDate = GETDATE() 
                    WHERE RecapID = @RecapID
                ", conn, tran);
                        updateCmd.Parameters.AddWithValue("@RecapID", entryId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Create new draft entry
                        var insertCmd = new SqlCommand(@"
                    INSERT INTO GMWeeklyRecapEntry (GMUserID, LocationID, WeekStartDate, CreatedDate, IsDraft)
                    OUTPUT INSERTED.RecapID
                    VALUES (@GMUserID, @LocationID, @WeekStartDate, GETDATE(), 1)
                ", conn, tran);

                        insertCmd.Parameters.AddWithValue("@GMUserID", user_id);
                        insertCmd.Parameters.AddWithValue("@LocationID", model.LocationID);
                        insertCmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

                        entryId = (int)await insertCmd.ExecuteScalarAsync();
                    }

                    // Save/Update draft fields
                    if (model.Fields != null)
                    {
                        foreach (var field in model.Fields.Where(f => !string.IsNullOrWhiteSpace(f.FieldValue)))
                        {
                            var upsertCmd = new SqlCommand(@"
                        MERGE GMWeeklyRecapField AS target
                        USING (SELECT @RecapID as RecapID, @FieldName as FieldName) AS source
                        ON target.RecapID = source.RecapID AND target.FieldName = source.FieldName
                        WHEN MATCHED THEN 
                            UPDATE SET FieldValue = @FieldValue, ModifiedDate = GETDATE()
                        WHEN NOT MATCHED THEN
                            INSERT (RecapID, FieldName, FieldValue, CreatedDate)
                            VALUES (@RecapID, @FieldName, @FieldValue, GETDATE());
                    ", conn, tran);

                            upsertCmd.Parameters.AddWithValue("@RecapID", entryId);
                            upsertCmd.Parameters.AddWithValue("@FieldName", field.FieldName);
                            upsertCmd.Parameters.AddWithValue("@FieldValue", field.FieldValue);

                            await upsertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await tran.CommitAsync();

                    // Invalidate cache after saving draft (since it affects the recap list)
                    _cache.Remove(RECAP_LIST_CACHE_KEY);

                    return Json(new { success = true, message = "Draft saved successfully" });
                }
                catch (Exception ex)
                {
                    await tran.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                await _errorLoggingService.LogErrorAsync(ex, HttpContext,
                    JsonSerializer.Serialize(model),
                    "GMRecap-SaveDraft");

                return Json(new { success = false, message = "Error saving draft" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFieldProgress()
        {
            try
            {
                var userClaim = HttpContext.User.FindFirst("Users_ID");
                if (userClaim == null || !int.TryParse(userClaim.Value, out int user_id))
                {
                    return Json(new { success = false, message = "Session expired" });
                }

                var location = HttpContext.User.FindFirst("LocationId")?.Value;
                int locationId = location != null ? int.Parse(location) : 0;

                var today = DateTime.Today;
                var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand(@"
            SELECT f.FieldName, f.FieldValue, f.CreatedDate
            FROM GMWeeklyRecapEntry e
            INNER JOIN GMWeeklyRecapField f ON e.RecapID = f.RecapID
            WHERE e.GMUserID = @GMUserID AND e.LocationID = @LocationID 
            AND e.WeekStartDate = @WeekStartDate
        ", conn);

                cmd.Parameters.AddWithValue("@GMUserID", user_id);
                cmd.Parameters.AddWithValue("@LocationID", locationId);
                cmd.Parameters.AddWithValue("@WeekStartDate", weekStart);

                var fields = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    fields.Add(new
                    {
                        fieldName = reader.GetString("FieldName"),
                        fieldValue = reader.IsDBNull("FieldValue") ? "" : reader.GetString("FieldValue"),
                        createdDate = reader.GetDateTime("CreatedDate")
                    });
                }

                return Json(new { success = true, fields = fields });
            }
            catch (Exception ex)
            {
                await _errorLoggingService.LogErrorAsync(ex, HttpContext,
                    null, "GMRecap-GetFieldProgress");

                return Json(new { success = false, message = "Error getting field progress" });
            }
        }
    }
}