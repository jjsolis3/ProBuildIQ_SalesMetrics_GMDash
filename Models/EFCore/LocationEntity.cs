using System;
using System.Collections.Generic;

namespace SalesMetrics.Models.EFCore;

public partial class LocationEntity
{
    public int LocationId { get; set; }

    public string LocationNumber { get; set; } = null!;

    public string LocationName { get; set; } = null!;

    public string LocationAbrv { get; set; } = null!;

    public bool IsActive { get; set; }
}
