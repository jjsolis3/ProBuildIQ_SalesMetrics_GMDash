using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesMetrics.Models;
using System.Data;
using static SalesMetrics.Models.Journal;

namespace SalesMetrics.Controllers
{
    public class JournalController : Controller
    {
        private readonly IConfiguration _configuration;

        public JournalController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET: /Journal/
        public IActionResult Index(int? categoryId)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var journalEntries = new List<Journal.JournalEntry>();

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var sql = @"
                SELECT J.Journal_ID, J.UserID, J.RoleID, J.LocationID, J.Title, J.NoteText, J.EntryDate, 
                       J.CreatedDate, J.ModifiedBy, J.ModifiedDate, C.Name as CategoryName, C.ColorClass as CategoryColor
                FROM JournalEntries J
                    LEFT JOIN JournalCategories C on J.CategoryID = C.CategoryID
                WHERE J.DeletedDate IS NULL AND J.UserID = @UserId";

                if (categoryId.HasValue)
                    sql += " AND J.CategoryID = @CategoryId";

                sql += " ORDER BY J.CreatedDate DESC";

                var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                if (categoryId.HasValue)
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId.Value);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    journalEntries.Add(new Journal.JournalEntry
                    {
                        Journal_ID = reader.GetInt32(0),
                        UserId = reader.GetInt32(1),
                        RoleId = reader.GetInt32(2),
                        LocationId = reader.GetInt32(3),
                        Title = reader["Title"]?.ToString(),
                        NoteText = reader["NoteText"]?.ToString(),
                        EntryDate = reader["EntryDate"] as DateTime?,
                        CreatedDate = reader.GetDateTime(7),
                        ModifiedBy = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                        ModifiedDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        CategoryName = reader["CategoryName"]?.ToString(),
                        CategoryColor = reader["CategoryColor"]?.ToString(),
                    });
                }
            }

            // Get all categories for the dropdown
            var categories = new List<JournalCategory>();
            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT CategoryID, Name FROM JournalCategories ORDER BY Name", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    categories.Add(new JournalCategory
                    {
                        CategoryID = reader.GetInt32(0),
                        Name = reader.GetString(1)
                    });
                }
            }

            ViewBag.Categories = categories;
            ViewBag.SelectedCategory = categoryId;
            return View(journalEntries);
        }


        // GET: /Journal/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Journal/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Journal.JournalEntry entry)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            entry.UserId = userId;
            entry.RoleId = roleId;
            entry.LocationId = locationId;
            entry.CreatedDate = DateTime.Now;

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    INSERT INTO JournalEntries (UserID, RoleID, LocationID, Title, NoteText, CategoryID, EntryDate, CreatedDate)
                    VALUES (@UserID, @RoleId, @LocationId, @Title, @NoteText, @CategoryId, @EntryDate, @CreatedDate)
                ", conn);

                cmd.Parameters.AddWithValue("@UserID", entry.UserId);
                cmd.Parameters.AddWithValue("@RoleId", entry.RoleId);
                cmd.Parameters.AddWithValue("@LocationId", entry.LocationId);
                cmd.Parameters.AddWithValue("@Title", entry.Title ?? "");
                cmd.Parameters.AddWithValue("@NoteText", entry.NoteText ?? "");
                cmd.Parameters.AddWithValue("@CategoryId", entry.CategoryId);
                cmd.Parameters.AddWithValue("@EntryDate", entry.EntryDate);
                cmd.Parameters.AddWithValue("@CreatedDate", entry.CreatedDate);

                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Journal entry added.";
            return RedirectToAction("Index");
        }

        // EDIT MODAL
        public IActionResult EditModal(int id)
        {
            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));
            Journal.JournalEntry entry = null;

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT * FROM JournalEntries 
                    WHERE Journal_ID = @Journal_ID 
                        AND DeletedDate IS NULL
                        AND LocationID = @LocationID

                ", conn);
                cmd.Parameters.AddWithValue("@Journal_ID", id);
                cmd.Parameters.AddWithValue("@LocationID", locationId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    entry = new Journal.JournalEntry
                    {
                        Journal_ID = reader.GetInt32(0),
                        UserId = reader.GetInt32(1),
                        RoleId = reader.GetInt32(2),
                        LocationId = reader.GetInt32(3),
                        Title = reader["Title"]?.ToString(),
                        NoteText = reader["NoteText"]?.ToString(),
                        CategoryId = reader.GetInt32(6),
                        EntryDate = reader.GetDateTime(7),
                        CreatedDate = reader.GetDateTime(8)
                    };
                }
            }

            if (entry == null)
                return NotFound();

            return PartialView("_EditModal", entry);
        }

        // GET: /Journal/Edit/5
        public IActionResult Edit(int id)
        {
            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));
            Journal.JournalEntry entry = null;

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT * FROM JournalEntries 
                    WHERE Journal_ID = @Journal_ID 
                        AND DeletedDate IS NULL,
                        AND LocationID = @LocationID
                ", conn);
                cmd.Parameters.AddWithValue("@Journal_ID", id);
                cmd.Parameters.AddWithValue("@LocationID", locationId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    entry = new Journal.JournalEntry
                    {
                        Journal_ID = reader.GetInt32(0),
                        UserId = reader.GetInt32(1),
                        RoleId = reader.GetInt32(2),
                        LocationId = reader.GetInt32(3),
                        Title = reader["Title"]?.ToString(),
                        NoteText = reader["NoteText"]?.ToString(),
                        CreatedDate = reader.GetDateTime(6)
                    };
                }
            }

            if (entry == null)
            {
                return NotFound();
            }

            return View(entry);
        }

        // POST: /Journal/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Journal.JournalEntry entry)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    UPDATE JournalEntries
                    SET Title = @Title,
                        NoteText = @NoteText,
                        ModifiedBy = @ModifiedBy,
                        ModifiedDate = @ModifiedDate
                    WHERE Journal_ID = @Journal_ID", conn);

                cmd.Parameters.AddWithValue("@Title", entry.Title ?? "");
                cmd.Parameters.AddWithValue("@NoteText", entry.NoteText ?? "");
                cmd.Parameters.AddWithValue("@ModifiedBy", userId);
                cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
                cmd.Parameters.AddWithValue("@Journal_ID", entry.Journal_ID);

                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Journal entry updated.";
            return RedirectToAction("Index");
        }

        // POST: /Journal/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    UPDATE JournalEntries
                    SET DeletedBy = @DeletedBy,
                        DeletedDate = @DeletedDate
                    WHERE Journal_ID = @Journal_ID", conn);

                cmd.Parameters.AddWithValue("@DeletedBy", userId);
                cmd.Parameters.AddWithValue("@DeletedDate", DateTime.Now);
                cmd.Parameters.AddWithValue("@Journal_ID", id);

                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Journal entry deleted.";
            return RedirectToAction("Index");
        }
    }
}
