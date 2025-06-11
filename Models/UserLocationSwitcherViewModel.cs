using Microsoft.AspNetCore.Mvc.Rendering;

namespace SalesMetrics.Models
{
    public class UserLocationSwitcherViewModel
    {
        public string CurrentLocation { get; set; }
        public List<SelectListItem> AvailableLocations { get; set; } = new();
    }
}

