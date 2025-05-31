namespace SalesMetrics.Models
{
    public class GMFullDashboardViewModel
    {
        public List<GMBranchSalesMetricsViewModel> BranchSalesMetrics { get; set; } = new();
        public GMARDataViewModel ARData { get; set; }
        public GMInstallerMetricsViewModel InstallerMetrics { get; set; }
        public GMInventoryDataViewModel InventoryData { get; set; }

        public DateTime MTDStartDateRange { get; set; }
        public DateTime YTDStartDateRange { get; set; }
        public DateTime EndDateRange { get; set; }

        public string? LocationFullName { get; set; }
    }

    public class GMBranchSalesMetricsViewModel
    {
        public string Location { get; set; }
        public decimal MTDSales { get; set; }
        public decimal YTDSales { get; set; }

        public int TotalOrders { get; set; }
        public int OnlineOrders { get; set; }

        public decimal TotalOrderAmount { get; set; }
        public decimal OnlineOrderAmount { get; set; }

        public double OnlineOrderPercentage => TotalOrders > 0
            ? (double)OnlineOrders / TotalOrders * 100 
            : 0;

        public string LocationFullName => Location switch
        {
            "LAX" => "Los Angeles",
            "LSV" => "Las Vegas",
            "CHN" => "Chino",
            "PHX" => "Phoenix",
            "SND" => "San Diego",
            _ => Location
        };

        public string MTDSalesFormatted => MTDSales.ToString("C0");
        public string YTDSalesFormatted => YTDSales.ToString("C0");
        public string OnlineOrderAmountFormatted => OnlineOrderAmount.ToString("C0");
        public string TotalOrderAmountFormatted => TotalOrderAmount.ToString("C0");
        public string OnlineOrderPercentageFormatted => $"{OnlineOrderPercentage:0.0}%";
    }
       

}

namespace SalesMetrics.Models
{
    public class GMARDataViewModel
    {
        public decimal DueUnder30 { get; set; }
        public decimal Due30to60 { get; set; }
        public decimal Due60to90 { get; set; }
        public decimal Due90to120 { get; set; }
        public decimal DueOver120 { get; set; }

        public decimal TotalAR => DueUnder30 + Due30to60 + Due60to90 + Due90to120 + DueOver120;

        public List<GMARChartSlice> ChartData => new List<GMARChartSlice>
            {
                new GMARChartSlice("Under 30 Days", DueUnder30),
                new GMARChartSlice("30–60 Days", Due30to60),
                new GMARChartSlice("60–90 Days", Due60to90),
                new GMARChartSlice("90–120 Days", Due90to120),
                new GMARChartSlice("120+ Days", DueOver120)
            }.Where(x => x.Value > 0).ToList();
        public string Location { get; set; }
    }

    public class GMARChartSlice
    {
        public GMARChartSlice(string label, decimal value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; set; }
        public decimal Value { get; set; }

    }

}

namespace SalesMetrics.Models
{
    public class GMInventoryDataViewModel
    {
        public int TotalItems { get; set; }
        public decimal TotalValuation { get; set; }

        public string TotalValuationFormatted => TotalValuation.ToString("C0");
        public string Location { get; set; }
    }
}

namespace SalesMetrics.Models
{
    public class GMInstallerMetricsViewModel
    {
        public int TotalInstallers { get; set; }
        public int ActiveInstallers { get; set; }
        public int OrdersCompletedThisMonth { get; set; }

        public double UtilizationPercentage => TotalInstallers > 0
            ? (double)ActiveInstallers / TotalInstallers * 100 : 0;

        public string UtilizationFormatted => $"{UtilizationPercentage:0.0}%";
        public string Location { get; set; }
    }

}
