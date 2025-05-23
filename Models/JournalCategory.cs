namespace SalesMetrics.Models
{
    public class JournalCategory
    {
        public int CategoryID { get; set; }
        public string Name { get; set; }
        public string? ColorClass { get; set; } // e.g. "badge bg-primary"
        public string? CategoryColor { get; set; } = "bg-secondary";

    }
}
