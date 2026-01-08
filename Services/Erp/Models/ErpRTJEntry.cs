namespace SalesMetrics.Services.Erp.Models
{
    /// <summary>
    /// Represents a Receive-Transfer-Journal (RTJ) inventory adjustment entry from any ERP
    /// </summary>
    public class ErpRTJEntry
    {
        /// <summary>
        /// Comments or description of the adjustment
        /// </summary>
        public string AdjustmentComments { get; set; } = string.Empty;

        /// <summary>
        /// Journal entry number
        /// </summary>
        public string JournalNumber { get; set; } = string.Empty;

        /// <summary>
        /// Credit amount
        /// </summary>
        public decimal Credit { get; set; }

        /// <summary>
        /// Debit amount
        /// </summary>
        public decimal Debit { get; set; }

        /// <summary>
        /// Transaction date
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Warehouse number where adjustment occurred
        /// </summary>
        public int WarehouseNumber { get; set; }

        /// <summary>
        /// Location code (LAX, LSV, CHN, PHX, SND, etc.)
        /// </summary>
        public string Location { get; set; } = string.Empty;
    }
}
