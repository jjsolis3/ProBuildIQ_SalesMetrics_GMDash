using System.Collections.Generic;
using System.Linq;

namespace SalesMetrics.Models
{
    /// <summary>
    /// Central definition of all GM Recap fields
    /// This is the SINGLE SOURCE OF TRUTH for all recap field definitions
    /// Used by: RecapEntry, RecapList, Controllers, Reports, etc.
    /// </summary>
    public static class GMRecapFieldDefinitions
    {
        /// <summary>
        /// Represents a single field definition
        /// </summary>
        public class FieldDefinition
        {
            public string Name { get; set; }
            public string Category { get; set; }
            public string Icon { get; set; }
        }

        /// <summary>
        /// All GM Recap fields - SINGLE SOURCE OF TRUTH
        /// To add/remove fields, ONLY update this list
        /// </summary>
        public static readonly FieldDefinition[] AllFields = new[]
        {
            // Performance (4 fields)
            new FieldDefinition { Name = "Wins", Category = "Performance", Icon = "🏆" },
            new FieldDefinition { Name = "Loses", Category = "Performance", Icon = "📉" },
            new FieldDefinition { Name = "Needs", Category = "Performance", Icon = "🎯" },
            new FieldDefinition { Name = "Actual vs Projected Sales #'s", Category = "Performance", Icon = "📊" },

            // Team (3 fields)
            new FieldDefinition { Name = "The Team", Category = "Team", Icon = "👥" },
            new FieldDefinition { Name = "1-1's with Sales Reps", Category = "Team", Icon = "💬" },
            new FieldDefinition { Name = "Ride-along with Sales Reps", Category = "Team", Icon = "🚗" },

            // Operations (5 fields)
            new FieldDefinition { Name = "Average Jobs per Day", Category = "Operations", Icon = "📈" },
            new FieldDefinition { Name = "Reschedules", Category = "Operations", Icon = "📅" },
            new FieldDefinition { Name = "Aged Inventory", Category = "Operations", Icon = "📦" },
            new FieldDefinition { Name = "Tablet %", Category = "Operations", Icon = "📱" },
            new FieldDefinition { Name = "Weekly Focus", Category = "Operations", Icon = "💡" },

            // Finance (2 fields)
            new FieldDefinition { Name = "A/R Update", Category = "Finance", Icon = "💰" },
            new FieldDefinition { Name = "Open Orders", Category = "Finance", Icon = "📋" },

            // Accounts (2 fields)
            new FieldDefinition { Name = "New Accounts", Category = "Accounts", Icon = "✨" },
            new FieldDefinition { Name = "Lost Accounts", Category = "Accounts", Icon = "❌" },

            // Customer Relations (3 fields)
            new FieldDefinition { Name = "Customer Events", Category = "Customer Relations", Icon = "🎉" },
            new FieldDefinition { Name = "Customer Visits", Category = "Customer Relations", Icon = "🏠" },
            new FieldDefinition { Name = "Ordering Issues", Category = "Customer Relations", Icon = "📢" }
        };

        /// <summary>
        /// Total number of fields (dynamically calculated)
        /// </summary>
        public static int TotalFieldCount => AllFields.Length;

        /// <summary>
        /// Get fields grouped by category
        /// </summary>
        public static IEnumerable<IGrouping<string, FieldDefinition>> GetFieldsByCategory()
        {
            return AllFields.GroupBy(f => f.Category);
        }

        /// <summary>
        /// Get all unique category names
        /// </summary>
        public static IEnumerable<string> GetCategories()
        {
            return AllFields.Select(f => f.Category).Distinct();
        }

        /// <summary>
        /// Get field count by category
        /// </summary>
        public static Dictionary<string, int> GetFieldCountByCategory()
        {
            return AllFields
                .GroupBy(f => f.Category)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// Check if a field name is valid
        /// </summary>
        public static bool IsValidFieldName(string fieldName)
        {
            return AllFields.Any(f => f.Name == fieldName);
        }

        /// <summary>
        /// Get field definition by name
        /// </summary>
        public static FieldDefinition GetFieldByName(string fieldName)
        {
            return AllFields.FirstOrDefault(f => f.Name == fieldName);
        }
    }
}
