namespace SalesMetrics.Models
{
    public class Calendar
    {
        public class CalendarUpdateModel
        {
            public int TaskId { get; set; }
            public string Title { get; set; }
            public DateTime NewDate { get; set; }
        }
    }


}
