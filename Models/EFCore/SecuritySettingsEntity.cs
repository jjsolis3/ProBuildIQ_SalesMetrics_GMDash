using System;

namespace SalesMetrics.Models.EFCore;

public partial class SecuritySettingsEntity
{
    public int SecuritySettingsId { get; set; }

    public string SettingKey { get; set; } = null!; // Unique key for the setting

    public string SettingValue { get; set; } = null!; // Value stored as string (JSON for complex values)

    public string? Description { get; set; } // Description of what this setting does

    public string Category { get; set; } = null!; // Security, Session, Password, General

    public DateTime LastModifiedDate { get; set; }

    public int LastModifiedByUserId { get; set; }
}
