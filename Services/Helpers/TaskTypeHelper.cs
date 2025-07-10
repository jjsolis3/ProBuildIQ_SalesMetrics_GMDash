using Microsoft.AspNetCore.Mvc.Rendering;

namespace SalesMetrics.Services.Helpers
{
    public class TaskTypeHelper
    {
        public static List<SelectListItem> GetTaskTypes(string source)
        {
            return source switch
            {
                "Task" or "AdminTask" or "Calendar" or "Schedule" => new List<SelectListItem>
                {
                    new("Measure", "Measure"),
                    new("Follow Up", "Follow Up"),
                    new("Quality Check", "Quality Check"),
                    new("Office - Bids", "Office - Bids"),
                    new("Office - Other", "Office - Other"),
                    new("Office - Paperwork", "Office - Paperwork"),
                    new("Other", "Other"),
                    new("Site Visit", "Site Visit"),
                    new("Walkthrough w/ Manager", "Walkthrough w/ Manager"),
                    new("Email", "Email"),
                    new("Phone Call", "Phone Call"),
                    new("QC Visit", "QC Visit"),
                    new("Escalation AR Follow-up", "Escalation AR Follow-up"),
                },

                "WorkOrder" or "Yardi" or "Property" => new List<SelectListItem>
                {
                    new("Follow Up", "Follow Up"),
                    new("Site Visit", "Site Visit"),
                    new("Quality Check", "Quality Check"),
                    new("Walkthrough w/ Manager", "Walkthrough w/ Manager"),
                    new("Email", "Email"),
                    new("Phone Call", "Phone Call"),
                    new("Other", "Other")
                },

                "Dashboard" or "AR" or "Inactive" => new List<SelectListItem>
                {
                    new("Follow Up", "Follow Up"),
                    new("Site Visit", "Site Visit"),
                    new("QC Visit", "QC Visit"),
                    new("Walkthrough w/ Manager", "Walkthrough w/ Manager"),
                    new("Email", "Email"),
                    new("Phone Call", "Phone Call"),
                    new("Escalation AR Follow-up", "Escalation AR Follow-up"),
                    new("Office - Bids", "Office - Bids"),
                    new("Office - Paperwork", "Office - Paperwork"),
                    new("Other", "Other")
                },

                _ => new List<SelectListItem>
                {
                    new("Measure", "Measure"),
                    new("Follow Up", "Follow Up"),
                    new("Site Visit", "Site Visit"),
                    new("Email", "Email"),
                    new("Phone Call", "Phone Call"),
                    new("Other", "Other")
                }
            };
        }
    }
}
