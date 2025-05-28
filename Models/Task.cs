using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models
{
    public class SalesTask
    {
        public int TaskID { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Status { get; set; } // e.g., "Pending", "In Progress", "Completed"
        public int? AssignedTo { get; set; }
        public int? Location { get; set; }  // NEW
        public string? Property { get; set; }  // NEW
        public int PropertyID { get; set; } // NEW
        public string? Type { get; set; }  // NEW
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }  // NEW
        public DateTime? CompletedDate { get; set; }  // NEW
        public DateTime? CancelledDate { get; set; }  // NEW
        public string? GoogleEventId { get; set; } // New property
        public int RoleId { get; set; }
    }

    public class TaskPageViewModel
    {
        public List<SalesTask>? Tasks { get; set; }
        public List<User>? Users { get; set; }
        public SalesTask? NewTask { get; set; }
    }

    public class TaskCreateViewModel
    {
        public int TaskID { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        // This is the combined value that will be saved
        public DateTime DueDate { get; set; }

        //These are form-only fields
        [BindProperty]
        public string? DueDateDate { get; set; }
        [BindProperty]
        public string? DueDateTime { get; set; }
        public DateTime TaskEndDate { get; set; } // New property for end date
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? Property { get; set; }
        public int PropertyID { get; set; }
        [Required]
        public int AssignedTo { get; set; }
    }

    public class TaskModalViewModel
    {
        public TaskCreateViewModel Task { get; set; }
        public List<User> Users { get; set; } = new();
        public int LoggedInUserId { get; set; }
        public int RoleId { get; set; }

        public List<SelectListItem> TaskTypes { get; set; } = new();
    }

    public class StatusUpdateModel
    {
        public int TaskId { get; set; }
        public string NewStatus { get; set; }
    }

    public class TaskCalendarViewModel
    {
        public int TaskId { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public string? Property { get; set; }
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }
        public string? AssignedTo { get; set; }
    }

    public class TaskNote
    {
        public int NoteID { get; set; }
        public int TaskID { get; set; }
        public string? NoteText { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? DeletedDate { get; set; }
    }

}