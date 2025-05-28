using System.Linq;

namespace SalesMetrics.Models.ViewModels
{
    public class GMDashboardViewModel
    {
        public decimal DueUnder30Amount { get; set; }
        public decimal Due30to60Amount { get; set; }
        public decimal Due60to90Amount { get; set; }
        public decimal Due90to120Amount { get; set; }
        public decimal DueOver120Amount { get; set; }

        public decimal TotalAR => DueUnder30Amount + Due30to60Amount + Due60to90Amount + Due90to120Amount + DueOver120Amount;

        public List<ARChartSlice> AgingChartData
        {
            get
            {
                var data = new List<ARChartSlice>();
                if (TotalAR == 0) return data;
                if (DueUnder30Amount > 0) data.Add(new ARChartSlice(" Under 30 Days", DueUnder30Amount));
                if (Due30to60Amount > 0) data.Add(new ARChartSlice(" 30–60 Days", Due30to60Amount));
                if (Due60to90Amount > 0) data.Add(new ARChartSlice(" 60–90 Days", Due60to90Amount));
                if (Due90to120Amount > 0) data.Add(new ARChartSlice(" 90–120 Days", Due90to120Amount));
                if (DueOver120Amount > 0) data.Add(new ARChartSlice(" 120+ Days", DueOver120Amount));

                return data;
            }
        }

        public decimal MTDSales { get; set; }
        public decimal YTDSales { get; set; }
        public DateTime MTDStartDateRange { get; set; }
        public DateTime YTDStartDateRange { get; set; }
        public DateTime EndDateRange { get; set; }

        public Dictionary<string, (List<SalesRanking> MTD, List<SalesRanking> YTD)> OfficeSalesRankings { get; set; } = new();

        public List<GMOfficeSalesRanking> GMOfficeSalesRankings { get; set; } = new();

        public List<GMOnlineOrders> OnlineOrders { get; set; } = new();
        public double OnlineOrderPercentageOverall => OnlineOrders.Sum(x => x.TotalOrders) > 0
            ? (double)OnlineOrders.Sum(x => x.OnlineOrders) / OnlineOrders.Sum(x => x.TotalOrders) * 100
            : 0;

    }

    public class ARChartSlice
    {
        public ARChartSlice(string label, decimal value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; set; }
        public decimal Value { get; set; }
    }

    public class GMOfficeSalesRanking
    {
        public int LocationID { get; set; }
        public string LocationName { get; set; }
        public decimal MTDSales { get; set; }
        public decimal YTDSales { get; set; }

        public string LocationFullName => LocationName switch
        {
            "LAX" => "Los Angeles",
            "LSV" => "Las Vegas",
            "CHN" => "Chino",
            "PHX" => "Phoenix",
            "SND" => "San Diego",
            _ => LocationName
        };

    }

    public class GMOnlineOrders
    {
        public int LocationID { get; set; }
        public string LocationName { get; set; }

        public int TotalOrders { get; set; }
        public int OnlineOrders { get; set; }

        public double OnlineOrderPercentage => TotalOrders > 0 
            ? (double)OnlineOrders / TotalOrders * 100 : 0;

        public decimal TotalOrdersAmount { get; set; }
        public decimal OnlineOrdersAmount { get; set; }
    }


}

