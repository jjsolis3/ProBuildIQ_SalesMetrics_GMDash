namespace SalesMetrics.Models
{
    public class Journal
    {
        public class JournalEntry
        {
            public int Journal_ID { get; set; }
            public int UserId { get; set; }
            public int RoleId { get; set; }
            public int LocationId { get; set; }
            public string? Title { get; set; }
            public string? NoteText { get; set; }
            public int? CategoryId { get; set; }
            public string? CategoryName { get; set; }
            public string? CategoryColor { get; set; }
            public DateTime? EntryDate { get; set; }
            public DateTime CreatedDate { get; set; }
            public int ModifiedBy { get; set; }
            public DateTime? ModifiedDate { get; set; }
            public int DeletedBy { get; set; }
            public DateTime? DeletedDate { get; set; }

        }

    }
}
