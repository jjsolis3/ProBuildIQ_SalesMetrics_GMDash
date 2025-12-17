namespace SalesMetrics.Services.Helpers
{
    // Helpers/DateRangeHelper.cs
    public static class DateRangeHelper
    {
        private const DayOfWeek WeekStart = DayOfWeek.Monday; // change to Sunday if you prefer

        public static (DateTime StartDate, DateTime EndDate) GetRange(string rangeKey)
        {
            var today = DateTime.Today;

            // Helper: start of this week based on WeekStart
            int diffToWeekStart = ((7 + (int)today.DayOfWeek - (int)WeekStart) % 7);
            DateTime thisWeekStart = today.AddDays(-diffToWeekStart).Date;

            switch (rangeKey?.ToLower())
            {
                case "week":
                case "thisweek":
                    return (thisWeekStart, today);

                case "lastweek":
                    {
                        var start = thisWeekStart.AddDays(-7);
                        var end = thisWeekStart.AddDays(-1); // day before this week starts
                        return (start, end);
                    }

                case "mtd":
                    return (new DateTime(today.Year, today.Month, 1), today);

                case "lastmonth":
                    {
                        var firstDayThisMonth = new DateTime(today.Year, today.Month, 1);
                        var firstDayLastMonth = firstDayThisMonth.AddMonths(-1);
                        var lastDayLastMonth = firstDayThisMonth.AddDays(-1);
                        return (firstDayLastMonth, lastDayLastMonth);
                    }

                case "thisquarter":
                    {
                        int startMonth = ((today.Month - 1) / 3) * 3 + 1; // 1,4,7,10
                        var start = new DateTime(today.Year, startMonth, 1);
                        return (start, today); // quarter-to-date
                    }

                case "lastquarter":
                    {
                        int thisQuarterStartMonth = ((today.Month - 1) / 3) * 3 + 1;
                        var thisQuarterStart = new DateTime(today.Year, thisQuarterStartMonth, 1);
                        var lastQuarterStart = thisQuarterStart.AddMonths(-3);
                        var lastQuarterEnd = thisQuarterStart.AddDays(-1);
                        return (lastQuarterStart, lastQuarterEnd);
                    }

                case "ytd":
                    return (new DateTime(today.Year, 1, 1), today);

                default:
                    // default to this week
                    return (thisWeekStart, today);
            }
        }
    }
}
