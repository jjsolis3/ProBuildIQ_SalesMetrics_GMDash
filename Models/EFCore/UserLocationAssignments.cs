using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models.EFCore
{
    public class UserLocationAssignment
    {
        [Key]
        public int AssignmentID { get; set; }
        public int UserID { get; set; }
        public int LocationID { get; set; }
        public string? IsActive { get; set; }
        public DateTime? DateAssigned { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }

        public LocationEntity? Location { get; set; } // FK relationship (optional)
    }
}
