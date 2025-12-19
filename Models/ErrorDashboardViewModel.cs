namespace SalesMetrics.Models
{
    public class ErrorDashboardViewModel
    {
        public int TotalErrors { get; set; }
        public int UnresolvedErrors { get; set; }
        public List<ErrorLogEntry> RecentErrors { get; set; } = new List<ErrorLogEntry>();
        public Dictionary<string, int> ErrorsByLevel { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ErrorsByController { get; set; } = new Dictionary<string, int>();
    }

    public class ErrorLogEntry
    {
        public int ErrorID { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Controller { get; set; }
        public bool IsResolved { get; set; }
    }
}