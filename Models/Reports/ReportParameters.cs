using System;
using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models.Reports
{
    public class ReportParameters
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public string? Location { get; set; }

        public int? SalesmanId { get; set; }

        public decimal? Threshold { get; set; }

        public decimal? MinMargin { get; set; }

        public decimal? TargetMargin { get; set; }

        public string? MgmtName { get; set; }

        public bool FilterByMgmt { get; set; }

        public int? WarehouseId { get; set; }
    }
}
