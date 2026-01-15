using SalesMetrics.Data.Entities.QueryBuilder;

namespace SalesMetrics.Models.QueryBuilder
{
    /// <summary>
    /// ViewModel for the Create Report wizard (multi-step process)
    /// </summary>
    public class ReportBuilderCreateViewModel
    {
        // Wizard state
        public int CurrentStep { get; set; } = 1;
        public int TotalSteps { get; set; } = 5;

        // Step 1: Table Selection
        public List<TableSelectionItem> AvailableTables { get; set; } = new();
        public List<string> SelectedTableNames { get; set; } = new();

        // Step 2: Column Configuration (will be used in next iteration)
        public List<ColumnSelectionItem> AvailableColumns { get; set; } = new();
        public List<string> SelectedColumnIds { get; set; } = new();

        // Step 3: Filter Configuration (will be used in next iteration)
        public List<FilterItem> Filters { get; set; } = new();

        // Step 4: Preview (will be used in next iteration)
        public string GeneratedSql { get; set; } = string.Empty;
        public List<Dictionary<string, object>> PreviewData { get; set; } = new();

        // Step 5: Save Configuration
        public string ReportName { get; set; } = string.Empty;
        public string ReportDescription { get; set; } = string.Empty;
        public string Category { get; set; } = "Custom";
        public string DataSourceType { get; set; } = "SQL";

        // User permissions
        public bool CanShare { get; set; }
        public bool IsAdmin { get; set; }
    }

    /// <summary>
    /// Represents a table available for selection
    /// </summary>
    public class TableSelectionItem
    {
        public int TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = "dbo";
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public int ColumnCount { get; set; }
        public string DataSourceType { get; set; } = "SQL";
    }

    /// <summary>
    /// Represents a column available for selection
    /// </summary>
    public class ColumnSelectionItem
    {
        public string ColumnId { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string TableDisplayName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsSelected { get; set; }
        public int DisplayOrder { get; set; }
        public int? SortOrder { get; set; } // 1, 2, 3, etc. or null if not sorting
        public string SortDirection { get; set; } = "ASC"; // ASC or DESC
        public string? AggregateFunction { get; set; } = null; // null, SUM, COUNT, AVG, MIN, MAX
        public string FormatString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a filter condition
    /// </summary>
    public class FilterItem
    {
        public string FilterId { get; set; } = Guid.NewGuid().ToString();
        public string ColumnName { get; set; } = string.Empty;
        public string Operator { get; set; } = "="; // =, !=, >, <, >=, <=, LIKE, IN, BETWEEN
        public string Value { get; set; } = string.Empty;
        public string LogicalOperator { get; set; } = "AND"; // AND, OR
    }

    /// <summary>
    /// DTO for Step 1 submission (Table Selection)
    /// </summary>
    public class Step1SubmissionDto
    {
        public List<int> SelectedTableIds { get; set; } = new();
    }

    /// <summary>
    /// DTO for Step 1.5 submission (Table Relationships)
    /// </summary>
    public class Step1_5SubmissionDto
    {
        public List<TableRelationship> Relationships { get; set; } = new();
    }

    /// <summary>
    /// Represents a relationship/join between two tables
    /// </summary>
    public class TableRelationship
    {
        public string LeftTable { get; set; } = string.Empty;
        public string LeftColumn { get; set; } = string.Empty;
        public string RightTable { get; set; } = string.Empty;
        public string RightColumn { get; set; } = string.Empty;
        public string JoinType { get; set; } = "INNER"; // INNER, LEFT, RIGHT, FULL
        public string? AdditionalCondition { get; set; } // Optional: e.g., "AND Status = 'Active'"
    }

    /// <summary>
    /// DTO for Step 2 submission (Column Configuration)
    /// </summary>
    public class Step2SubmissionDto
    {
        public List<ColumnSelectionItem> SelectedColumns { get; set; } = new();
    }

    /// <summary>
    /// DTO for column schema from INFORMATION_SCHEMA
    /// </summary>
    public class ColumnSchemaDto
    {
        public string? ColumnName { get; set; }
        public string? DataType { get; set; }
        public string? IsNullable { get; set; }
        public int OrdinalPosition { get; set; }
    }

    /// <summary>
    /// Represents a table with its columns for relationship building
    /// </summary>
    public class TableWithColumns
    {
        public string TableName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<TableColumnInfo> Columns { get; set; } = new();
    }

    /// <summary>
    /// Basic column information for relationship building
    /// </summary>
    public class TableColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
    }
}
