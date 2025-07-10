using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace SalesMetrics.Controllers
{
    public class GMRecapController : Controller
    {
        private readonly IConfiguration _configuration;

        public GMRecapController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult RecapEntry()
        {
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            var model = new GMRecapCardViewModel
            {
                WeekStartDate = weekStart
            };

            return View("RecapEntry", model);
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
            var skippedFields = new List<string>();

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                int entryId;

                // 🔍 Check if entry already exists for the week
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
                }
                else
                {
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

                // 🔁 Loop over fields
                foreach (var field in model.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.FieldValue))
                        continue;

                    var checkFieldCmd = new SqlCommand(@"
                        SELECT FieldValue FROM GMWeeklyRecapField
                        WHERE RecapID = @RecapID AND FieldName = @FieldName
                    ", conn, tran);

                    checkFieldCmd.Parameters.AddWithValue("@RecapID", entryId);
                    checkFieldCmd.Parameters.AddWithValue("@FieldName", field.FieldName);

                    var existingValueObj = await checkFieldCmd.ExecuteScalarAsync();
                    if (existingValueObj != null)
                    {
                        var existingValue = existingValueObj.ToString();
                        if (string.Equals(existingValue, field.FieldValue, StringComparison.OrdinalIgnoreCase))
                        {
                            skippedFields.Add(field.FieldName);
                            continue;
                        }

                        var insertFieldCmd = new SqlCommand(@"
                            INSERT INTO GMWeeklyRecapField (RecapID, FieldName, FieldValue, CreatedDate)
                            VALUES (@RecapID, @FieldName, @FieldValue, GETDATE())
                        ", conn, tran);
                        insertFieldCmd.Parameters.AddWithValue("@RecapID", entryId);
                        insertFieldCmd.Parameters.AddWithValue("@FieldName", field.FieldName);
                        insertFieldCmd.Parameters.AddWithValue("@FieldValue", field.FieldValue);
                        await insertFieldCmd.ExecuteNonQueryAsync();
                    }
                    else
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
                }

                await tran.CommitAsync();

                if (skippedFields.Any())
                {
                    TempData["PartialSubmittedWarning"] = $"These fields were already identical and skipped: {string.Join(", ", skippedFields)}.";
                }
                else
                {
                    TempData["RecapSubmitted"] = "true";
                }

                return RedirectToAction("RecapEntry");
            }
            catch
            {
                await tran.RollbackAsync();
                TempData["ErrorMessage"] = "An error occurred while saving your recap.";
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
                SELECT E.RecapID, E.WeekStartDate, U.FirstName + ' ' + U.LastName as GMName, E.CreatedDate
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
                    GMName = reader.GetString(2),
                    CreatedDate = reader.GetDateTime(3),
                    Fields = new List<RecapField>() // to be populated after
                });
            }

            reader.Close();

            // Then, get all fields
            foreach (var recap in recapCards)
            {
                var fieldCmd = new SqlCommand(@"
                    SELECT FieldName, 
                        FieldValue 
                    FROM GMWeeklyRecapField 
                    WHERE RecapID = @RecapID
                ", conn);
                fieldCmd.Parameters.AddWithValue("@RecapID", recap.RecapID);

                using var fieldReader = await fieldCmd.ExecuteReaderAsync();
                while (await fieldReader.ReadAsync())
                {
                    recap.Fields.Add(new RecapField
                    {
                        FieldName = fieldReader.GetString(0),
                        FieldValue = fieldReader.IsDBNull(1) ? "" : fieldReader.GetString(1)
                    });
                }

                fieldReader.Close();
            }

            return View("RecapList", recapCards);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecapDetails(int recapId)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var entry = new GMRecapCardViewModel();
            var cmd = new SqlCommand(@"
                SELECT RecapID, 
                    WeekStartDate, 
                    GMUserID, 
                    LocationID, 
                    CreatedDate 
                FROM GMWeeklyRecapEntry 
                WHERE RecapID = @RecapID
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
            }
            reader.Close();

            var fieldCmd = new SqlCommand(@"
                SELECT FieldName, 
                    FieldValue,
                    CreatedDate
                FROM GMWeeklyRecapField 
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

        [HttpPost]
        public async Task<IActionResult> DeleteRecap(int recapId)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();
            try
            {
                // Step 1: Delete fields first (in case cascade is not enforced)
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
                // Optional: update ModifiedDate on the main recap entry
                var updateRecapEntry = new SqlCommand(@"
                    UPDATE GMWeeklyRecapEntry 
                    SET ModifiedDate = GETDATE(),
                        ModifiedBy = 
                    WHERE RecapID = @RecapID
                ", conn, tran);
                updateRecapEntry.Parameters.AddWithValue("@RecapID", model.RecapID);
                await updateRecapEntry.ExecuteNonQueryAsync();

                foreach (var field in model.Fields)
                {
                    var updateFieldCmd = new SqlCommand(@"
                        UPDATE GMWeeklyRecapField 
                        SET FieldValue = @FieldValue
                        WHERE RecapID = @RecapID AND FieldName = @FieldName
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
            catch
            {
                await tran.RollbackAsync();
                TempData["ErrorMessage"] = "Error occurred while updating the recap.";
                return RedirectToAction("RecapList");
            }
        }

        private async Task<List<string>> GetDuplicateFieldsAsync(GMRecapCardViewModel model)
        {
            var duplicates = new List<string>();
            var connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT FieldName, FieldValue
                FROM GMWeeklyRecapEntry E
                INNER JOIN GMWeeklyRecapField F ON E.RecapID = F.RecapID
                WHERE E.GMUserID = @GMUserID AND E.LocationID = @LocationID AND E.WeekStartDate = @WeekStartDate
            ", conn);

            cmd.Parameters.AddWithValue("@GMUserID", model.GMUserID);
            cmd.Parameters.AddWithValue("@LocationID", model.LocationID);
            cmd.Parameters.AddWithValue("@WeekStartDate", model.WeekStartDate);

            using var reader = await cmd.ExecuteReaderAsync();
            var existingPairs = new HashSet<(string Field, string Value)>(new FieldValueTupleComparer());
            while (await reader.ReadAsync())
            {
                var field = reader.GetString(0);
                var value = reader.IsDBNull(1) ? "" : reader.GetString(1);
                existingPairs.Add((field, value));
            }

            foreach (var field in model.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.FieldValue) &&
                    existingPairs.Contains((field.FieldName, field.FieldValue)))
                {
                    duplicates.Add(field.FieldName);
                }
            }

            return duplicates;
        }

    }

    public class GMWeeklyEntrySummary
    {
        public int RecapID { get; set; }
        public DateTime WeekStartDate { get; set; }
        public string GMName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class FieldValueTupleComparer : IEqualityComparer<(string Field, string Value)>
    {
        public bool Equals((string Field, string Value) x, (string Field, string Value) y)
        {
            return string.Equals(x.Field, y.Field, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Field, string Value) obj)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (obj.Field?.ToLowerInvariant().GetHashCode() ?? 0);
                hash = hash * 23 + (obj.Value?.ToLowerInvariant().GetHashCode() ?? 0);
                return hash;
            }
        }
    }

}
