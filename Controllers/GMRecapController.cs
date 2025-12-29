using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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

        // RESTORED: Constructor with IErrorLoggingService dependency injection
        public GMRecapController(IConfiguration configuration, IErrorLoggingService errorLoggingService)
        {
            _configuration = configuration;
            _errorLoggingService = errorLoggingService;
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

        // RESTORED: Helper method to load existing recap data for visual indicators
        private async Task LoadExistingRecapData(GMRecapCardViewModel model)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Check for existing entry this week
            var checkCmd = new SqlCommand(@"
                SELECT RecapID FROM GMWeeklyRecapEntry
                WHERE GMUserID = @GMUserID AND LocationID = @LocationID AND WeekStartDate = @WeekStartDate
            ", conn);
            checkCmd.Parameters.AddWithValue("@GMUserID", model.GMUserID);
            checkCmd.Parameters.AddWithValue("@LocationID", model.LocationID);
            checkCmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

            var existingRecapId = await checkCmd.ExecuteScalarAsync();
            if (existingRecapId != null)
            {
                int recapId = (int)existingRecapId;
                model.RecapID = recapId;

                // Load existing fields for visual indicator (badges, progress bar)
                var fieldCmd = new SqlCommand(@"
                    SELECT FieldName, FieldValue, CreatedDate 
                    FROM GMWeeklyRecapField 
                    WHERE RecapID = @RecapID
                    ORDER BY CreatedDate DESC
                ", conn);
                fieldCmd.Parameters.AddWithValue("@RecapID", recapId);

                using var reader = await fieldCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.Fields.Add(new RecapField
                    {
                        FieldName = reader.GetString(0),
                        FieldValue = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        CreatedDate = reader.GetDateTime(2)
                    });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitWeeklyRecap(GMRecapCardViewModel model)
        {
            bool hasValidFields = model.Fields.Any(f => !string.IsNullOrWhiteSpace(f.FieldValue));
            if (!hasValidFields)
            {
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

                // Check if entry already exists for the week
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

                    // Update the ModifiedDate on the main entry
                    var updateEntryCmd = new SqlCommand(@"
                        UPDATE GMWeeklyRecapEntry SET ModifiedDate = GETDATE() WHERE RecapID = @RecapID
                    ", conn, tran);
                    updateEntryCmd.Parameters.AddWithValue("@RecapID", entryId);
                    await updateEntryCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // Create new entry
                    var insertEntryCmd = new SqlCommand(@"
                        INSERT INTO GMWeeklyRecapEntry (GMUserID, LocationID, WeekStartDate, CreatedDate)
                        OUTPUT INSERTED.RecapID
                        VALUES (@GMUserID, @LocationID, @WeekStartDate, GETDATE())
                    ", conn, tran);
                    insertEntryCmd.Parameters.AddWithValue("@GMUserID", user_id);
                    insertEntryCmd.Parameters.AddWithValue("@LocationID", model.LocationID);
                    insertEntryCmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

                    entryId = (int)await insertEntryCmd.ExecuteScalarAsync();
                }

                // ========================================================================
                // CRITICAL FIX: ALWAYS INSERT new field entries (never update from Entry page)
                // This ensures each submission creates separate entries that can be tracked
                // individually and displayed with proper counts in RecapList.
                // ========================================================================
                foreach (var field in model.Fields.Where(f => !string.IsNullOrWhiteSpace(f.FieldValue)))
                {
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
                TempData["RecapSubmitted"] = "true";
                return RedirectToAction("RecapEntry");
            }
            catch (SqlException sqlEx)
            {
                await tran.RollbackAsync();

                // RESTORED: SQL-specific error logging with additional context
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

                // RESTORED: General error logging with additional context
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
            var location = HttpContext.User.FindFirst("OfficeLocation")?.Value;
            var connStr = _configuration.GetConnectionString("SalesMetrics");

            var recapCards = new List<GMRecapCardViewModel>();

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // First, get all recaps
            var recapCmd = new SqlCommand(@"
                SELECT E.RecapID, E.WeekStartDate, U.FirstName + ' ' + U.LastName as GMName, E.CreatedDate, E.GMUserID
                FROM GMWeeklyRecapEntry E
                LEFT JOIN Users U ON E.GMUserID = U.Users_ID
                ORDER BY E.WeekStartDate DESC
            ", conn);

            using var reader = await recapCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                recapCards.Add(new GMRecapCardViewModel
                {
                    RecapID = reader.GetInt32(0),
                    WeekStartDate = reader.GetDateTime(1),
                    GMName = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                    CreatedDate = reader.GetDateTime(3),
                    GMUserID = reader.GetInt32(4),
                    Fields = new List<RecapField>()
                });
            }
            reader.Close();

            // Then fetch fields for each recap
            foreach (var card in recapCards)
            {
                var fieldCmd = new SqlCommand(@"
                    SELECT FieldName, FieldValue, CreatedDate 
                    FROM GMWeeklyRecapField 
                    WHERE RecapID = @RecapID
                    ORDER BY CreatedDate DESC
                ", conn);
                fieldCmd.Parameters.AddWithValue("@RecapID", card.RecapID);

                using var fieldReader = await fieldCmd.ExecuteReaderAsync();
                while (await fieldReader.ReadAsync())
                {
                    card.Fields.Add(new RecapField
                    {
                        FieldName = fieldReader.GetString(0),
                        FieldValue = fieldReader.IsDBNull(1) ? "" : fieldReader.GetString(1),
                        CreatedDate = fieldReader.GetDateTime(2)
                    });
                }
            }

            // RESTORED: Get current user ID for "Your Entry" badge
            var currentUserId = HttpContext.User.FindFirst("Users_ID")?.Value;
            ViewBag.CurrentUserId = currentUserId != null ? int.Parse(currentUserId) : 0;

            return View("RecapList", recapCards);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecapDetails(int recapId)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            var recap = new GMRecapCardViewModel { Fields = new List<RecapField>() };

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // Get recap header info
            var recapCmd = new SqlCommand(@"
                SELECT E.RecapID, E.WeekStartDate, U.FirstName + ' ' + U.LastName as GMName, E.CreatedDate
                FROM GMWeeklyRecapEntry E
                LEFT JOIN Users U ON E.GMUserID = U.Users_ID
                WHERE E.RecapID = @RecapID
            ", conn);
            recapCmd.Parameters.AddWithValue("@RecapID", recapId);

            using var reader = await recapCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                recap.RecapID = reader.GetInt32(0);
                recap.WeekStartDate = reader.GetDateTime(1);
                recap.GMName = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2);
                recap.CreatedDate = reader.GetDateTime(3);
            }
            reader.Close();

            // Fetch all field entries (showing all separate entries, not just latest)
            var fieldCmd = new SqlCommand(@"
                SELECT FieldName, FieldValue, CreatedDate 
                FROM GMWeeklyRecapField 
                WHERE RecapID = @RecapID
                ORDER BY FieldName, CreatedDate DESC
            ", conn);
            fieldCmd.Parameters.AddWithValue("@RecapID", recapId);

            using var fieldReader = await fieldCmd.ExecuteReaderAsync();
            while (await fieldReader.ReadAsync())
            {
                recap.Fields.Add(new RecapField
                {
                    FieldName = fieldReader.GetString(0),
                    FieldValue = fieldReader.IsDBNull(1) ? "" : fieldReader.GetString(1),
                    CreatedDate = fieldReader.GetDateTime(2)
                });
            }

            return PartialView("_RecapDetailsPartial", recap);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRecap(int recapId)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                // Step 1: Delete fields first (child records)
                var deleteFieldsCmd = new SqlCommand(
                    "DELETE FROM GMWeeklyRecapField WHERE RecapID = @RecapID", conn, tran);
                deleteFieldsCmd.Parameters.AddWithValue("@RecapID", recapId);
                await deleteFieldsCmd.ExecuteNonQueryAsync();

                // Step 2: Delete the entry
                var deleteEntryCmd = new SqlCommand(
                    "DELETE FROM GMWeeklyRecapEntry WHERE RecapID = @RecapID", conn, tran);
                deleteEntryCmd.Parameters.AddWithValue("@RecapID", recapId);
                await deleteEntryCmd.ExecuteNonQueryAsync();

                await tran.CommitAsync();
                TempData["SuccessMessage"] = "Recap entry deleted successfully.";
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                await _errorLoggingService.LogErrorAsync(ex, HttpContext, null, "GMRecap-DeleteRecap");
                TempData["ErrorMessage"] = "Error deleting recap entry: " + ex.Message;
            }

            return RedirectToAction("RecapList");
        }

        [HttpGet]
        public async Task<IActionResult> EditRecapModal(int recapId)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var recap = new GMRecapCardViewModel();

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
            }
            reader.Close();

            // RESTORED: Fetch LATEST fields using ROW_NUMBER for editing
            // This ensures we only show the most recent entry per field for editing
            recap.Fields = new List<RecapField>();
            var fieldCmd = new SqlCommand(@"
                WITH RankedFields AS (
                    SELECT FieldName, FieldValue, CreatedDate,
                           ROW_NUMBER() OVER (PARTITION BY FieldName ORDER BY CreatedDate DESC) as rn
                    FROM GMWeeklyRecapField 
                    WHERE RecapID = @RecapID
                )
                SELECT FieldName, FieldValue, CreatedDate
                FROM RankedFields
                WHERE rn = 1
            ", conn);
            fieldCmd.Parameters.AddWithValue("@RecapID", recapId);

            using var fieldReader = await fieldCmd.ExecuteReaderAsync();
            while (await fieldReader.ReadAsync())
            {
                recap.Fields.Add(new RecapField
                {
                    FieldName = fieldReader.GetString(0),
                    FieldValue = fieldReader.IsDBNull(1) ? "" : fieldReader.GetString(1),
                    CreatedDate = fieldReader.GetDateTime(2)
                });
            }

            return PartialView("_EditRecapModal", recap);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRecap(GMRecapCardViewModel model)
        {
            var loggedInUser = HttpContext.User.FindFirst("Users_ID")?.Value;
            if (model.RecapID <= 0 || model.Fields == null || !model.Fields.Any())
            {
                TempData["ErrorMessage"] = "Invalid recap data submitted.";
                return RedirectToAction("RecapList");
            }

            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                // FIXED: Update ModifiedDate and ModifiedBy on the main recap entry
                var updateRecapEntry = new SqlCommand(@"
                    UPDATE GMWeeklyRecapEntry 
                    SET ModifiedDate = GETDATE(),
                        ModifiedBy = @ModifiedBy
                    WHERE RecapID = @RecapID
                ", conn, tran);
                updateRecapEntry.Parameters.AddWithValue("@RecapID", model.RecapID);
                updateRecapEntry.Parameters.AddWithValue("@ModifiedBy", loggedInUser ?? (object)DBNull.Value);
                await updateRecapEntry.ExecuteNonQueryAsync();

                // RESTORED: Update only the LATEST entry for each field (using subquery)
                foreach (var field in model.Fields)
                {
                    var updateFieldCmd = new SqlCommand(@"
                        UPDATE GMWeeklyRecapField 
                        SET FieldValue = @FieldValue
                        WHERE RecapID = @RecapID 
                          AND FieldName = @FieldName
                          AND CreatedDate = (
                              SELECT MAX(CreatedDate) 
                              FROM GMWeeklyRecapField 
                              WHERE RecapID = @RecapID AND FieldName = @FieldName
                          )
                    ", conn, tran);
                    updateFieldCmd.Parameters.AddWithValue("@RecapID", model.RecapID);
                    updateFieldCmd.Parameters.AddWithValue("@FieldName", field.FieldName);
                    updateFieldCmd.Parameters.AddWithValue("@FieldValue", field.FieldValue ?? "");

                    await updateFieldCmd.ExecuteNonQueryAsync();
                }

                await tran.CommitAsync();
                TempData["SuccessMessage"] = "Recap updated successfully.";
                return RedirectToAction("RecapList");
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();
                await _errorLoggingService.LogErrorAsync(ex, HttpContext, null, "GMRecap-UpdateRecap");
                TempData["ErrorMessage"] = "Error occurred while updating the recap.";
                return RedirectToAction("RecapList");
            }
        }
    }
}