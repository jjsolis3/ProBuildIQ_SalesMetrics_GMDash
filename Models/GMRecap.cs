namespace SalesMetrics.Models
{
    public class GMRecapCardViewModel
    {
        public int RecapID { get; set; }
        public int GMUserID { get; set; }
        public int LocationID { get; set; }
        public string GMName { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public List<RecapField> Fields { get; set; } = new();
    }

    public class RecapField
    {
        public int FieldID { get; set; }  // Added to track individual field entries
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
