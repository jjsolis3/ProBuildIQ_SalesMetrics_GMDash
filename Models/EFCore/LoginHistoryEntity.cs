using System;
using System.Collections.Generic;

namespace SalesMetrics.Models.EFCore;

public partial class LoginHistoryEntity
{
    public int LoginHistoryId { get; set; }

    public int UserId { get; set; }

    public string UserName { get; set; } = null!;

    public DateOnly LoginDate { get; set; }

    public DateTime LoginTime { get; set; }

    public string? Success { get; set; }

    public string? Ipaddress { get; set; }

    public string? Office { get; set; }

    public string? DeviceInfo { get; set; }

    public string? UserAgentRaw { get; set; }

    public string? ErrorLog { get; set; }
}
