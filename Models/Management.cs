using System;
using System.Collections.Generic;

namespace SalesMetrics.Models
{
    // Enhanced ViewModel for the Management Dashboard
    // This includes both the list of companies and aggregate KPIs
    public class ManagementDashboardViewModel
    {
        // List of all management companies with their metrics
        public List<ManagementCompanyListItem> Companies { get; set; }

        // Aggregate KPIs across all management companies
        public int TotalCompanies => Companies?.Count ?? 0;
        public int TotalProperties => Companies?.Sum(c => c.PropertyCount) ?? 0;
        public int TotalInvoices => Companies?.Sum(c => c.InvoiceCount) ?? 0;

        // YTD metrics
        public int TotalThisYearInvoices => Companies?.Sum(c => c.ThisYearInvoiceCount) ?? 0;
        public decimal TotalThisYearRevenue => Companies?.Sum(c => c.ThisYearRevenue) ?? 0;

        public int TotalLastYearInvoices => Companies?.Sum(c => c.LastYearInvoiceCount) ?? 0;
        public decimal TotalLastYearRevenue => Companies?.Sum(c => c.LastYearRevenue) ?? 0;

        // All-time metrics
        public decimal TotalRevenue => Companies?.Sum(c => c.TotalRevenue) ?? 0;
        public decimal TotalARDue => Companies?.Sum(c => c.AmountDue) ?? 0;

        // Calculated KPIs
        public double OverallGainLossPercentage
        {
            get
            {
                if (TotalLastYearRevenue == 0)
                {
                    return TotalThisYearRevenue > 0 ? 100.0 : 0.0;
                }
                return (double)(((TotalThisYearRevenue - TotalLastYearRevenue) / TotalLastYearRevenue) * 100);
            }
        }

        public double CollectionRate
        {
            get
            {
                if (TotalRevenue == 0) return 0;
                return (double)(((TotalRevenue - TotalARDue) / TotalRevenue) * 100);
            }
        }

        // Top performers (sorted by This Year Revenue)
        public List<ManagementCompanyListItem> TopPerformers =>
            Companies?.OrderByDescending(c => c.ThisYearRevenue).Take(5).ToList() ?? new List<ManagementCompanyListItem>();
    }

    // This model represents a single management company in the list view
    // It includes aggregated metrics for all properties under this management company
    public class ManagementCompanyListItem
    {
        public int PriceCode { get; set; }
        public string Name { get; set; }
        public int PropertyCount { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AmountDue { get; set; }
        public int ThisYearInvoiceCount { get; set; }
        public int LastYearInvoiceCount { get; set; }
        public decimal ThisYearRevenue { get; set; }
        public decimal LastYearRevenue { get; set; }

        // Rank based on This Year Revenue (set by controller)
        public int Rank { get; set; }

        // Calculated property: Gain/Loss percentage comparing this year to last year
        // This is computed on-the-fly and doesn't require database storage
        public double GainLossPercentage
        {
            get
            {
                // Avoid division by zero
                if (LastYearRevenue == 0)
                {
                    // If there was no revenue last year but there is this year, show 100% gain
                    return ThisYearRevenue > 0 ? 100.0 : 0.0;
                }

                // Calculate the percentage change: ((ThisYear - LastYear) / LastYear) * 100
                return (double)(((ThisYearRevenue - LastYearRevenue) / LastYearRevenue) * 100);
            }
        }
    }

    // This model represents the detailed view of a management company
    // It includes KPIs, monthly trends, and a list of all properties
    public class ManagementDetailsViewModel
    {
        public int PriceCode { get; set; }
        public string Name { get; set; }

        // KPI metrics
        public int PropertyCount { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AmountDue { get; set; }
        public decimal ThisYearRevenue { get; set; }
        public decimal LastYearRevenue { get; set; }

        // Calculated property: Gain/Loss percentage for the KPI card
        public double GainLossPercentage
        {
            get
            {
                if (LastYearRevenue == 0)
                {
                    return ThisYearRevenue > 0 ? 100.0 : 0.0;
                }
                return (double)(((ThisYearRevenue - LastYearRevenue) / LastYearRevenue) * 100);
            }
        }

        // Monthly invoice data for the trend chart
        // This list will be used to create a dual-line chart comparing this year vs last year
        public List<MonthlyInvoiceSummary> MonthlyInvoices { get; set; }

        // List of all properties under this management company
        public List<ManagementPropertyItem> Properties { get; set; }

        // Helper methods to get monthly data split by year for the chart
        // These methods filter the MonthlyInvoices list to separate this year and last year data
        public List<MonthlyInvoiceSummary> ThisYearMonthlyData
        {
            get
            {
                var currentYear = DateTime.Now.Year;
                return MonthlyInvoices.Where(m => m.InvoiceYear == currentYear.ToString()).ToList();
            }
        }

        public List<MonthlyInvoiceSummary> LastYearMonthlyData
        {
            get
            {
                var lastYear = DateTime.Now.Year - 1;
                return MonthlyInvoices.Where(m => m.InvoiceYear == lastYear.ToString()).ToList();
            }
        }
    }

    // This model represents a single property under a management company
    // It includes enhanced metrics for year-over-year comparison
    public class ManagementPropertyItem
    {
        public int PropertyId { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Salesperson { get; set; }
        public decimal ARBalance { get; set; }
        public int InvoiceCount { get; set; }
        public decimal ThisYearRevenue { get; set; }
        public decimal LastYearRevenue { get; set; }

        // Calculated property: Gain/Loss percentage for this property
        public double GainLossPercentage
        {
            get
            {
                if (LastYearRevenue == 0)
                {
                    return ThisYearRevenue > 0 ? 100.0 : 0.0;
                }
                return (double)(((ThisYearRevenue - LastYearRevenue) / LastYearRevenue) * 100);
            }
        }
    }
}