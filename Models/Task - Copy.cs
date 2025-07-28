//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.ModelBinding;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using System.ComponentModel.DataAnnotations;

//namespace SalesMetrics.Models
//{
//    public class SalesTask
//    {
//        public int TaskID { get; set; }
//        public string? Title { get; set; }
//        public string? Description { get; set; }
//        public DateTime? DueDate { get; set; }
//        public string? Status { get; set; } // e.g., "Pending", "In Progress", "Completed"
//        public int? AssignedTo { get; set; }
//        public int? Location { get; set; }  // NEW
//        public string? Property { get; set; }  // NEW
//        public int? PropertyID { get; set; } // NEW
//        public string? Type { get; set; }  // NEW
//        public string? CreatedBy { get; set; }
//        public DateTime? CreatedDate { get; set; }
//        public DateTime? ModifiedDate { get; set; }  // NEW
//        public DateTime? CompletedDate { get; set; }  // NEW
//        public DateTime? CancelledDate { get; set; }  // NEW
//        public string? GoogleEventId { get; set; } // New property
//        public int RoleId { get; set; }
//        public int? CreatedById { get; set; } // User ID of the creator
//        // add Google Sync Bit [IsSyncedToGoogle]
//        public bool IsSyncedToGoogle { get; set; } // Indicates if the task is synced with Google Calendar

//    }

//    public class TaskPageViewModel
//    {
//        public List<SalesTask>? Tasks { get; set; }
//        public List<User>? Users { get; set; }
//        public SalesTask? NewTask { get; set; }
//    }

//    public class TaskCreateViewModel
//    {
//        public int TaskID { get; set; }
//        [Required(ErrorMessage = "A Task Title is required.")]
//        public string? Title { get; set; }
//        public string? Description { get; set; }

//        // These two fields are used for separate date/time input
//        [BindProperty]
//        public string DueDateDate { get; set; }
//        [BindProperty]
//        public string DueDateTime { get; set; }

//        // Final merged datetime field used when saving
//        [BindNever]
//        public DateTime DueDate { get; set; }

//        public DateTime TaskEndDate { get; set; } // New property for end date

//        [Required(ErrorMessage = "A Task Type is required.")]
//        public string Type { get; set; }
//        [Required(ErrorMessage = "A Task Status is required.")]
//        public string Status { get; set; }

//        public string? Property { get; set; }
//        public int? PropertyID { get; set; }
        
//        public int AssignedTo { get; set; }
//        public string Source { get; set; } // e.g., "Task", "WorkOrder", "Yardi", etc.

//        // Optional context-specific fields
//        public int? DaysInactive { get; set; }
//        public decimal? Balance { get; set; }
//        public string? OrderID { get; set; }
//        public string? Unit { get; set; }
//        public int LocationID { get; set; }
//    }

//    public class TaskModalViewModel
//    {
//        public TaskCreateViewModel Task { get; set; }
//        public List<User> Users { get; set; } = new();
//        public int LoggedInUserId { get; set; }
//        public int RoleId { get; set; }

//        public List<SelectListItem> TaskTypes { get; set; } = new();
//    }

//    public class StatusUpdateModel
//    {
//        public int TaskId { get; set; }
//        public string NewStatus { get; set; }
//    }

//    public class TaskCalendarViewModel
//    {
//        public int TaskId { get; set; }
//        public string Title { get; set; }
//        public string Type { get; set; }
//        public string Status { get; set; }
//        public string? Property { get; set; }
//        public string? Description { get; set; }
//        public DateTime? DueDate { get; set; }
//        public string? AssignedTo { get; set; }
//    }

//    public class TaskNote
//    {
//        public int NoteID { get; set; }
//        public int TaskID { get; set; }
//        public string? NoteText { get; set; }
//        public string? CreatedBy { get; set; }
//        public DateTime CreatedDate { get; set; }
//        public string? ModifiedBy { get; set; }
//        public DateTime? ModifiedDate { get; set; }
//        public string? DeletedBy { get; set; }
//        public DateTime? DeletedDate { get; set; }
//    }

//}