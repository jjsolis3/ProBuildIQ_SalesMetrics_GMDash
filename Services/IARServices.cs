using SalesMetrics.Models;
public interface IARService
{
    Task<GMARDataViewModel> GetARDataAsync(string location, int whsId);
    Task<List<GMARDataViewModel>> GetARDataForAllBranchesAsync(int locationId, int whsId);
}

// Services/IInventoryService.cs
public interface IInventoryService
{
    Task<List<BranchInventorySummary>> GetInventorySummaryByBranchAsync(string officeLocation);
    List<InventoryProductClassSummary> GetInventoryCostByBranch(string location, int warehouseId);
}

// Services/IInstallerService.cs
public interface IInstallerService
{
    Task<List<InstallerCompletionMetric>> GetInstallerMetricsForWeekAsync(string location, DateTime startDate, DateTime endDate);
    Task<List<InstallerDetails>> GetInstallerDetailEntriesAsync(DateTime startDate, DateTime endDate, string location);
}

// Services/IRTJService.cs
public interface IRTJService
{
    Task<List<RTJEntry>> GetRecentRTJEntriesAsync(DateTime startDate, DateTime endDate, string location);
}