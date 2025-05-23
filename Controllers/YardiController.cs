using Microsoft.AspNetCore.Mvc;
using ExcelDataReader;
using System.Data;
using System.Data.SqlTypes;
using SalesMetrics.Data;

using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using Microsoft.Extensions.Caching.Memory;

namespace SalesMetrics.Controllers
{
    public class YardiController : Controller
    {
        private readonly SalesMetricsDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;

        // Injected constructor for DB context, environment, and caching
        public YardiController(SalesMetricsDbContext context, IWebHostEnvironment env, IMemoryCache cache)
        {
            _context = context;
            _env = env;
            _cache = cache;
        }


        [HttpGet]
        public IActionResult Upload()
        {
            return View();
        }

        [HttpGet]
        public IActionResult YardiProperties()
        {

            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            int parsedUserId = int.Parse(userId);

            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int locationId = int.Parse(User.FindFirst("LocationId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            ViewBag.UserId = parsedUserId;
            ViewBag.RoleId = roleId;
            ViewBag.LocationId = locationId;

            List<SalesMetrics.Models.User> userList;

            if (roleId == 2) // Sales - only see yourself
            {
                var user = _context.Users
                    .Where(u => u.UserId == parsedUserId && u.IsActive == true)
                    .Select(u => new SalesMetrics.Models.User
                    {
                        UserID = u.UserId,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        RoleID = u.RoleId,
                    })
                    .FirstOrDefault();

                userList = user != null ? new List<SalesMetrics.Models.User> { user } : new List<SalesMetrics.Models.User>();
            }
            else // Admins and Sales Admins
            {
                userList = _context.Users
                    .Where(u => u.IsActive == true && u.RoleId == 2 && u.Location == locationId)
                    .Select(u => new SalesMetrics.Models.User
                    {
                        UserID = u.UserId,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        RoleID = u.RoleId
                    })
                    .OrderBy(u => u.FirstName)
                    .ToList();
            }

            ViewBag.Users = userList;

            try { 
                List<YardiPropertyViewModel> properties;

                if (!_cache.TryGetValue("yardi_property_list", out properties))
                {
                    properties = _context.YardiProperties
                        .OrderByDescending(p => p.ImportedDate)
                        .Take(3000) // Optional: limit for performance if needed
                        .Select(p => new YardiPropertyViewModel
                        {
                            Property_ID = p.Property_ID,
                            Market = p.Market,
                            Submarket = p.Submarket,
                            YardiID = p.YardiID,
                            PropertyName = p.PropertyName,
                            PropertyAddress = p.PropertyAddress,
                            PropertyCity = p.PropertyCity,
                            PropertyState = p.PropertyState,
                            PropertyZipCode = p.PropertyZipCode,
                            PropertyPhone = p.PropertyPhone,
                            PropertyStatus = p.PropertyStatus,
                            ImprRating = p.ImprRating,
                            LocRating = p.LocRating,
                            OwnerFName = p.OwnerFName,
                            OwnerLName = p.OwnerLNname,
                            OwnerEmail = p.OwnerEmail,
                            OwnverAddress = p.OwnverAddress,
                            OwnverCity = p.OwnverCity,
                            OwnverState = p.OwnverState,
                            OwnverZipCode = p.OwnverZipCode,
                            OwnerPhone = p.OwnerPhone,
                            OwnerWebsite = p.OwnerWebsite,
                            Manager = p.Manager,
                            ManagerFName = p.ManagerFName,
                            ManagerLName = p.ManagerLName,
                            ManagerAddress = p.ManagerAddress,
                            ManagerCity = p.ManagerCity,
                            ManagerState = p.ManagerState,
                            ManagerZIP = p.ManagerZIP,
                            ManagerPhone = p.ManagerPhone,
                            ManagerWebsite = p.ManagerWebsite,
                            PropertyType = p.PropertyType,
                            ConstructionType = p.ConstructionType,
                            Amenities = p.Amenities,
                            Notes = p.Notes,
                            ImportedBy = p.ImportedBy,
                            Units = p.Units != 0 ? p.Units : 0,
                            SqFt = p.SqFt,
                            CompletionDate = p.CompletionDate,
                            Latitude = p.Latitude,
                            Longitude = p.Longitude,
                            OccupancyRate = p.OccupancyRate,
                            AvgAskingRent = p.AvgAskingRent,
                            YearBuilt = p.YearBuilt,
                            YearRenovated = p.YearRenovated,
                            ImportedDate = p.ImportedDate.HasValue ? p.ImportedDate.Value : (DateTime?)null
                        })
                        .ToList();

                    // Cache for 10 minutes
                    _cache.Set("yardi_property_list", properties, TimeSpan.FromMinutes(10));
                }

                var allKeys = HttpContext.Session.Keys.ToList();
                foreach (var key in allKeys)
                {
                    Console.WriteLine($"{key} = {HttpContext.Session.GetString(key)}");
                }

                return View(properties);
            }
            catch (SqlNullValueException ex)
            {
                Console.WriteLine("[YardiProperties] SQL Null Error: " + ex.Message);
                throw;
            }
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please upload a valid Excel file.";
                return RedirectToAction("Upload");
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet();
            var table = result.Tables[0];

            var importedBy = User.Identity?.Name ?? "system";

            var columnCount = table.Columns.Count;

            for (int i = 1; i < table.Rows.Count; i++)
            {
                var row = table.Rows[i];
                var prop = new YardiPropertyEntity
                {
                    Market = Truncate(row[0]?.ToString(), 255),
                    Submarket = Truncate(row[1]?.ToString(), 100),
                    YardiID = Truncate(row[2]?.ToString(), 100),
                    PropertyName = Truncate(row[3]?.ToString(), 255),
                    PropertyAddress = Truncate(row[4]?.ToString(), 255),
                    PropertyCity = Truncate(row[5]?.ToString(), 100),
                    PropertyState = Truncate(row[6]?.ToString(), 50),
                    PropertyZipCode = Truncate(row[7]?.ToString(), 20),
                    PropertyPhone = Truncate(row[8]?.ToString(), 50),
                    PropertyStatus = Truncate(row[9]?.ToString(), 255),

                    // Continue this pattern for every string field
                    ImprRating = Truncate(row[13]?.ToString(), 10),
                    LocRating = Truncate(row[14]?.ToString(), 10),

                    OwnerFName = Truncate(row[26]?.ToString(), 255),
                    OwnerLNname = Truncate(row[27]?.ToString(), 255),
                    OwnerEmail = Truncate(row[28]?.ToString(), 255),
                    OwnverAddress = Truncate(row[29]?.ToString(), 255),
                    OwnverCity = Truncate(row[30]?.ToString(), 100),
                    OwnverState = Truncate(row[31]?.ToString(), 50),
                    OwnverZipCode = Truncate(row[32]?.ToString(), 20),
                    OwnerPhone = Truncate(row[34]?.ToString(), 50),
                    OwnerWebsite = Truncate(row[35]?.ToString(), 255),

                    Manager = Truncate(row[45]?.ToString(), 255),
                    ManagerFName = Truncate(row[46]?.ToString(), 255),
                    ManagerLName = Truncate(row[47]?.ToString(), 255),
                    ManagerAddress = Truncate(row[48]?.ToString(), 255),
                    ManagerCity = Truncate(row[49]?.ToString(), 100),
                    ManagerState = Truncate(row[50]?.ToString(), 50),
                    ManagerZIP = Truncate(row[51]?.ToString(), 20),
                    ManagerPhone = Truncate(row[52]?.ToString(), 50),
                    ManagerWebsite = Truncate(row[53]?.ToString(), 255),

                    //PropertyType = Truncate(row[37]?.ToString(), 100),
                    //ConstructionType = Truncate(row[38]?.ToString(), 100),
                    //Amenities = Truncate(row[39]?.ToString(), 1000), // if nvarchar(max), you can leave as-is or trim to avoid surprise
                    //Notes = Truncate(row[40]?.ToString(), 1000),
                    ImportedBy = Truncate(importedBy, 100),

                    // Keep parsing logic for numbers/dates
                    Units = int.TryParse(row[10]?.ToString(), out var units) ? units : null,
                    SqFt = double.TryParse(row[11]?.ToString(), out var sqft) ? sqft : null,
                    CompletionDate = DateTime.TryParse(row[12]?.ToString(), out var dt) ? dt : null,
                    Latitude = double.TryParse(row[57]?.ToString(), out var lat) ? lat : null,
                    Longitude = double.TryParse(row[58]?.ToString(), out var lng) ? lng : null,
                    //OccupancyRate = decimal.TryParse(row[36]?.ToString(), out var occ) ? occ : null,
                    //AvgAskingRent = decimal.TryParse(row[36]?.ToString(), out var rent) ? rent : null,
                    //YearBuilt = int.TryParse(row[41]?.ToString(), out var yb) ? yb : null,
                    //YearRenovated = int.TryParse(row[42]?.ToString(), out var yr) ? yr : null,
                    ImportedDate = DateTime.Now
                };
                _context.YardiProperties.Add(prop);
            }

            _context.SaveChanges();

            TempData["Success"] = "Yardi property data uploaded successfully!";
            return RedirectToAction("Upload");
        }

        private string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

    }
}
