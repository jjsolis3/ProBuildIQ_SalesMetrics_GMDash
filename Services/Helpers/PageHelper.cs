namespace SalesMetrics.Services.Helpers
{
    public static class PageHelper
    {
        public static bool ShouldHideLocationSwitcher(string controller, string action)
        {
            var pagesToExclude = new List<(string Controller, string Action)>
        {
            ("GMDash", "Index"),
            ("GMRecap", "RecapEntry"),
            ("GMRecap", "RecapList")
        };

            return pagesToExclude.Any(p =>
                string.Equals(p.Controller, controller, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Action, action, StringComparison.OrdinalIgnoreCase)
            );
        }
    }

}
