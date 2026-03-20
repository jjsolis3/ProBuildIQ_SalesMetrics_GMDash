namespace SalesMetrics.Models
{
    public class ErrorDashboardViewModel
    {
        public int TotalErrors { get; set; }
        public int UnresolvedErrors { get; set; }
        public List<ErrorLogEntry> RecentErrors { get; set; } = new List<ErrorLogEntry>();
        public Dictionary<string, int> ErrorsByLevel { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ErrorsByController { get; set; } = new Dictionary<string, int>();

        /// <summary>Login failure statistics surfaced from LoginHistory.</summary>
        public LoginSecurityStats LoginSecurity { get; set; } = new LoginSecurityStats();
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

    public class LoginSecurityStats
    {
        public int FailedLoginsLast24h { get; set; }
        public int FailedLoginsLast7d { get; set; }
        public List<LoginFailureEntry> RecentFailures { get; set; } = new List<LoginFailureEntry>();
        public List<TopFailedIpEntry> TopFailedIps { get; set; } = new List<TopFailedIpEntry>();
    }

    public class LoginFailureEntry
    {
        public DateTime LoginTime { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? Office { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class TopFailedIpEntry
    {
        public string IpAddress { get; set; } = string.Empty;
        public int FailureCount { get; set; }
    }
}