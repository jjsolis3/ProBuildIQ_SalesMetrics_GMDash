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
    }
}
