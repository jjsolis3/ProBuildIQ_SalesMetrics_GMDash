using System;
using System.Collections.Generic;

namespace SalesMetrics.Models.EFCore;

public partial class TaskEntity
{
    public int TaskId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime DueDate { get; set; }

    public string? Status { get; set; }

    public int AssignedTo { get; set; }

    public int Location { get; set; }

    public string? Property { get; set; }
    public int PropertyID { get; set; }

    public string? Type { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime? CancelledDate { get; set; }
}
