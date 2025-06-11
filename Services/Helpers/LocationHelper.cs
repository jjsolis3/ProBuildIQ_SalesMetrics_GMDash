// Helpers/LocationHelper.cs
namespace SalesMetrics.Services.Helpers
{
    public static class LocationHelper
    {
        public static Dictionary<int, string> GetLocationMap()
        {
            return new Dictionary<int, string>
            {
                { 1, "LAX" },
                { 2, "LSV" },
                { 3, "CHN" },
                { 4, "PHX" },
                { 5, "SND" }
            };
        }

        public static List<string> GetLocationQueryList(int locationId)
        {
            var map = GetLocationMap();
            return locationId == 0 ? map.Values.ToList() : new List<string> { map[locationId] };
        }

        public static string? GetConnectionName(int locationId)
        {
            var map = GetLocationMap();
            return map.ContainsKey(locationId) ? map[locationId] : null;
        }

        public static int GetLocationId(string locationName)
        {
            return GetLocationMap().FirstOrDefault(x => x.Value == locationName).Key;
        }

        public static Dictionary<int, (string Code, string Name)> Locations = new()
        {
            { 1, ("LAX", "Los Angeles") },
            { 2, ("LSV", "Las Vegas") },
            { 3, ("CHN", "Chino") },
            { 4, ("PHX", "Phoenix") },
            { 5, ("SND", "San Diego") }
        };

        public static string? GetLocationCode(int id) => Locations.ContainsKey(id) ? Locations[id].Code : null;
        public static string GetLocationName(int id) => Locations.ContainsKey(id) ? Locations[id].Name : "Unknown";

        public static string GetBranchCode(int locationId)
        {
            return locationId switch
            {
                1 => "LAX",
                2 => "LSV",
                3 => "CHN",
                4 => "PHX",
                5 => "SND",
                _ => "LAX"
            };
        }

        public static int GetCurrentLocationId(HttpContext context)
        {
            return int.TryParse(context.Session.GetString("LocationId"), out var locId) ? locId : 0;
        }

        public static string GetCurrentOfficeCode(HttpContext context)
        {
            return context.Session.GetString("OfficeLocation") ?? "LAX";
        }

    }
}
