using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Client;

namespace SalesMetrics.Models
{
    public class GMFullDashboardViewModel
    {
        public List<GMBranchSalesMetricsViewModel> BranchSalesMetrics { get; set; } = new();
        public GMARDataViewModel ARData { get; set; }
        public GMInstallerMetricsViewModel InstallerMetrics { get; set; }
        public GMInventoryDataViewModel InventoryData { get; set; }
        public List<InventoryProductClassSummary> InventoryByClass { get; set; } = new();
        public List<InstallerCompletionMetric> InstallerCompletionMetrics { get; set; } = new();
        public List<GMARDataViewModel> ARBranchBreakdown { get; set; } = new();
        public List<RTJEntry> RecentRTJs { get; set; } = new();


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
        public int TotalRegularOrders => TotalOrders - OnlineOrders;

        public decimal TotalOrderAmount { get; set; }
        public decimal OnlineOrderAmount { get; set; }
        public decimal TotalRegularOrdersAmount => TotalOrderAmount - OnlineOrderAmount;
        public string TotalRegularOrderAmountFormatted => TotalRegularOrdersAmount.ToString("C");

        public double OnlineOrderPercentage => TotalOrders > 0
            ? (double)OnlineOrderAmount / (double)TotalOrderAmount * 100 
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

    public class GMInventoryDataViewModel
    {
        public int TotalItems { get; set; }
        public decimal TotalValuation { get; set; }

        public string TotalValuationFormatted => TotalValuation.ToString("C0");
        public string Location { get; set; }
    }

    public class BranchInventorySummary
    {
        public string OfficeBranch { get; set; }
        public string WarehouseId { get; set; }
        public string ProductClass { get; set; }
        public string Style { get; set; }
        public string Color { get; set; }
        public string Description { get; set; }
        public string Vendor { get; set; }
        public string RollLot { get; set; }
        public string UOM { get; set; } // Unit of Measure
        public string Units { get; set; } // Units / Carton
        public string Pieces { get; set; } // Pieces / Carton
        public string RollWidth { get; set; } // Roll Width in Inches
        public decimal QtyAvailable { get; set; }
        public decimal QtyOnHand { get; set; }
        public decimal QtyAllocated { get; set; }
        public int AgingDays { get; set; }
        public DateTime DateReceived { get; set; }
    }

    public class InventoryProductClassSummary
    {
        public string WarehouseId { get; set; }
        public string ProductClass { get; set; }
        public decimal LAX_InventoryCost { get; set; }
        public decimal LSV_InventoryCost { get; set; }
        public decimal CHN_InventoryCost { get; set; }
        public decimal PHX_InventoryCost { get; set; }
        public decimal SND_InventoryCost { get; set; }
    }

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

    public class InstallerCompletionMetric
    {
        public string Location { get; set; }

        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }

        public double CompletionRate => TotalOrders > 0
            ? Math.Round((double)CompletedOrders / TotalOrders * 100, 2)
            : 0;

        public int WhsArrivalCount { get; set; }
        public int WhsDepartCount { get; set; }
        public int OrderArrivalCount { get; set; }
        public int OrderCompletedCount { get; set; }
        public int OrderCancelledCount { get; set; }
    }

    public class InstallerDetails
    {
        public string Property { get; set; } // Property Name
        public string? PropertyId { get; set; } // Property ID

        public string Installer { get; set; }
        public int InstallerId { get; set; } // Installer ID
        public int OrderId { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? WhsArrive { get; set; }
        public DateTime? MaterialConfirm { get; set; }
        public string? WhsLoading { get; set; } // Warehouse Time
        public string? DistanceFromWhs { get; set; } // Distance from Warehouse
        public string? TravelTime { get; set; } // Travel Time to Job Site
        public string? JobLocation { get; set; } // Job Address
        public DateTime? JobArrival { get; set; }
        public DateTime? JobCompleted { get; set; }
        public DateTime? JobDepart { get; set; }
        public string? JobCompleteLocation { get; set; } // Job Completion Address
        public string? JobDuration { get; set; } // Job Duration
        public DateTime? JobCancelled { get; set; } // Job Cancelled Date

        // Calculate Time Between JobArrival and Job Completed
        public TimeSpan? TimeToCompleteJob => JobCompleted > JobArrival
            ? JobCompleted - JobArrival
            : (TimeSpan?)null;

        // Calculate Time between WhsArrival and JobArrival
        public TimeSpan? TimeToJobArrival => JobArrival > WhsArrive
            ? JobArrival - WhsArrive
            : (TimeSpan?)null;



    }

    public class RTJEntry
    {
        public string AdjustmentComments { get; set; }
        public string JournalNumber { get; set; }
        public decimal Credit { get; set; }
        public decimal Debit { get; set; }
        public DateTime Date { get; set; }
        public int WarehouseNumber { get; set; }
        public string Location { get; set; }  // NEW ✅
    }




}
