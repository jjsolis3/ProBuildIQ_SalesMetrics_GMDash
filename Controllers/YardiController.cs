using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExcelDataReader;
using System.Data;
using System.Data.SqlTypes;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using Microsoft.Extensions.Caching.Memory;
using SalesMetrics.Services.Helpers;

namespace SalesMetrics.Controllers
{
    [Authorize]
    public class YardiController : Controller
    {
        private readonly SalesMetricsDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ILogger<YardiController> _logger;

        public YardiController(SalesMetricsDbContext context, IWebHostEnvironment env, IMemoryCache cache, ILogger<YardiController> logger)
        {
            _context = context;
            _env = env;
            _cache = cache;
            _logger = logger;
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
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            //int locationId = int.Parse(User.FindFirst("LocationId")?.Value ?? "0");
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
                string cacheKey = $"yardi_property_list_{locationId}";

                if (!_cache.TryGetValue(cacheKey, out properties))
                {
                    string locationKey = LocationHelper.GetLocationCode(locationId);

                    properties = _context.YardiProperties
                        .Where(p => p.Locations.Contains(locationKey))
                        .OrderByDescending(p => p.ImportedDate)
                        .Take(5000) // Optional: limit for performance if needed
                        .Select(p => new YardiPropertyViewModel
                        {
                            Property_ID = p.Property_ID,
                            Market = p.Market,
                            Submarket = p.Submarket,
                            YardiID = p.YardiID,
                            PropertyName = p.PropertyName,
                            PropertyAddress = p.PropertyAddress,
                            PropertyCity = p.PropertyCity,
                            PropertyCounty = p.PropertyCounty,
                            PropertyState = p.PropertyState,
                            PropertyZipCode = p.PropertyZipCode,
                            PropertyPhone = p.PropertyPhone,
                            PropertyStatus = p.PropertyStatus,
                            Units = p.Units != 0 ? p.Units : 0,
                            SqFt = p.SqFt,
                            CompletionDate = p.CompletionDate,
                            ImprRating = p.ImprRating,
                            LocRating = p.LocRating,
                            Owner = p.Owner,
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
                            PropertyNotes = p.PropertyNotes,
                            OwnerNotes = p.OwnerNotes,
                            ManagerNotes = p.ManagerNotes,
                            YearBuilt = p.YearBuilt,
                            YearRenovated = p.YearRenovated,
                            AvgAskingRent = p.AvgAskingRent,
                            OccupancyRate = p.OccupancyRate,
                            PropertyType = p.PropertyType,
                            ConstructionType = p.ConstructionType,
                            Amenities = p.Amenities,
                            Notes = p.Notes,
                            Latitude = p.Latitude,
                            Longitude = p.Longitude,
                            ImportedBy = p.ImportedBy,
                            ImportedDate = p.ImportedDate.HasValue ? p.ImportedDate.Value : (DateTime?)null,
                            Locations = p.Locations,
                        })
                        .ToList();

                    // Cache for 10 minutes
                    _cache.Set(cacheKey, properties, TimeSpan.FromMinutes(10));
                }

                var allKeys = HttpContext.Session.Keys.ToList();
                _logger.LogDebug("YardiProperties session keys: {Keys}", string.Join(", ", allKeys));

                return View(properties);
            }
            catch (SqlNullValueException ex)
            {
                _logger.LogError(ex, "YardiProperties SQL null error");
                throw;
            }
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file, List<string> selectedLocations)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please upload a valid Excel file.";
                return RedirectToAction("Upload");
            }

            string locationCsv = selectedLocations != null && selectedLocations.Any()
                ? string.Join(",", selectedLocations)
                : ""; // Default fallback


            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet();
            var table = result.Tables[0];
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);

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
                    PropertyCounty = Truncate(row[6]?.ToString(), 100),
                    PropertyState = Truncate(row[7]?.ToString(), 50),
                    PropertyZipCode = Truncate(row[8]?.ToString(), 20),
                    PropertyPhone = Truncate(row[9]?.ToString(), 50),
                    PropertyStatus = Truncate(row[10]?.ToString(), 255),

                    // Continue this pattern for every string field
                    ImprRating = Truncate(row[14]?.ToString(), 10),
                    LocRating = Truncate(row[15]?.ToString(), 10),

                    Owner = Truncate(row[26]?.ToString(), 255),
                    OwnerFName = Truncate(row[27]?.ToString(), 255),
                    OwnerLNname = Truncate(row[28]?.ToString(), 255),
                    OwnerEmail = Truncate(row[29]?.ToString(), 255),
                    OwnverAddress = Truncate(row[30]?.ToString(), 255),
                    OwnverCity = Truncate(row[31]?.ToString(), 100),
                    OwnverState = Truncate(row[32]?.ToString(), 50),
                    OwnverZipCode = Truncate(row[33]?.ToString(), 20),
                    OwnerPhone = Truncate(row[35]?.ToString(), 50),
                    OwnerWebsite = Truncate(row[36]?.ToString(), 255),

                    Manager = Truncate(row[46]?.ToString(), 255),
                    ManagerFName = Truncate(row[47]?.ToString(), 255),
                    ManagerLName = Truncate(row[48]?.ToString(), 255),
                    ManagerAddress = Truncate(row[49]?.ToString(), 255),
                    ManagerCity = Truncate(row[50]?.ToString(), 100),
                    ManagerState = Truncate(row[51]?.ToString(), 50),
                    ManagerZIP = Truncate(row[52]?.ToString(), 20),
                    ManagerPhone = Truncate(row[53]?.ToString(), 50),
                    ManagerWebsite = Truncate(row[54]?.ToString(), 255),
                    PropertyNotes = Truncate(row[55]?.ToString(), 1000),
                    OwnerNotes = Truncate(row[56]?.ToString(), 1000),
                    ManagerNotes = Truncate(row[57]?.ToString(), 1000),

                    // Keep parsing logic for numbers/dates
                    Units = int.TryParse(row[11]?.ToString(), out var units) ? units : null,
                    SqFt = double.TryParse(row[12]?.ToString(), out var sqft) ? sqft : null,
                    CompletionDate = DateTime.TryParse(row[13]?.ToString(), out var dt) ? dt : null,
                    Latitude = double.TryParse(row[58]?.ToString(), out var lat) ? lat : null,
                    Longitude = double.TryParse(row[59]?.ToString(), out var lng) ? lng : null,

                    AvgAskingRent = decimal.TryParse(row[60]?.ToString(), out var rent) ? rent : null,
                    OccupancyRate = decimal.TryParse(row[61]?.ToString(), out var occ) ? occ : null,

                    PropertyType = "MultiFamily",
                    //ConstructionType = Truncate(row[17]?.ToString(), 100),
                    //Amenities = Truncate(row[18]?.ToString(), 1000),
                    //Notes = Truncate(row[19]?.ToString(), 1000),

                    ImportedBy = Truncate(importedBy, 100),
                    ImportedDate = DateTime.Now,
                    Locations = locationCsv

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
