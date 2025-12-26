using System;
using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public string NotificationType { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Message { get; set; }
        public string? ActionUrl { get; set; }
        public int? RelatedTaskId { get; set; }
        public int? RelatedEnvelopeId { get; set; }
        public int? BroadcastMessageId { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsSystemGenerated { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadDate { get; set; }
        public string? CreatedByName { get; set; } // For display
    }

    public class NotificationListViewModel
    {
        public List<Notification> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class BroadcastMessageCreateViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Message is required.")]
        public string Message { get; set; } = null!;

        [Required(ErrorMessage = "Target type is required.")]
        public string TargetType { get; set; } = null!; // AllUsers, Location, Role

        public int? TargetLocationId { get; set; }

        public int? TargetRoleId { get; set; }

        public DateTime? ExpiresDate { get; set; }

        public string Priority { get; set; } = "Normal"; // Normal, High, Urgent
    }

    public class BroadcastMessage
    {
        public int BroadcastMessageId { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string TargetType { get; set; } = null!;
        public int? TargetLocationId { get; set; }
        public int? TargetRoleId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiresDate { get; set; }
        public bool IsActive { get; set; }
        public string? Priority { get; set; }
        public string? CreatedByName { get; set; }
        public string? TargetName { get; set; } // Display name of target (location or role name)
    }

    public class NotificationMarkReadRequest
    {
        public int NotificationId { get; set; }
        public bool IsRead { get; set; }
    }

    public class NotificationDeleteRequest
    {
        public int NotificationId { get; set; }
    }
}
