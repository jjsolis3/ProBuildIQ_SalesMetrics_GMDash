# Controller Refactoring Guide

## 🎯 Purpose

This guide shows how to refactor existing controllers and services to use the new ERP abstraction layer instead of direct SQL queries.

---

## ✅ Completed Refactoring

### **ErpMergeService** (Services/Signing/ErpMergeService.cs)

**Status:** ✅ Partially Complete
- ✅ GetPropertyNameAsync() - migrated
- ✅ GetPropertyAddressAsync() - migrated
- ✅ SearchPropertiesAsync() - migrated
- ⏳ GetOrdersForPropertyAsync() - TODO (complex joins)
- ⏳ GetOrderByIdAsync() - TODO (complex joins)

---

## 📖 Refactoring Pattern

### **Before (Direct SQL)**

```csharp
public class MyController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<IActionResult> GetProperty(int propertyId)
    {
        // Get location from session
        var locationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";

        // Get connection string
        var connStr = _configuration.GetConnectionString(locationCode);

        // Open connection
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Execute query
        var sql = @"
            SELECT
                CUM_CUMMAS_ID as CustomerId,
                CUM_CUSTOMER_NAME as CustomerName,
                CUM_ADDRESS_1 as Address,
                CUM_CITY as City
            FROM CUSTOMER_MASTER
            WHERE CUM_CUMMAS_ID = @propertyId";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@propertyId", propertyId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var property = new PropertyViewModel
            {
                CustomerId = reader.GetInt32(0),
                CustomerName = reader.GetString(1),
                Address = reader.GetString(2),
                City = reader.GetString(3)
            };
            return View(property);
        }

        return NotFound();
    }
}
```

**Problems:**
- ❌ Hardcoded to SQL Server (can't switch to Kudu)
- ❌ Repeated connection boilerplate
- ❌ Manual parameter mapping
- ❌ Manual result mapping
- ❌ Location-dependent connection strings
- ❌ Hard to test (requires real database)

---

### **After (Abstraction Layer)**

```csharp
public class MyController : Controller
{
    private readonly ErpClientFactory _erpFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MyController(ErpClientFactory erpFactory, IHttpContextAccessor httpContextAccessor)
    {
        _erpFactory = erpFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    private ErpContext GetErpContext()
    {
        var locationCode = _httpContextAccessor.HttpContext?.Session.GetString("OfficeLocation") ?? "LAX";
        return new ErpContext { LocationCode = locationCode };
    }

    public async Task<IActionResult> GetProperty(int propertyId)
    {
        // Get ERP client for current location
        var context = GetErpContext();
        var client = _erpFactory.GetClient(context);

        // Get property using abstraction layer
        var property = await client.GetPropertyByIdAsync(propertyId, context);

        if (property == null)
        {
            return NotFound();
        }

        // Map to ViewModel
        var viewModel = new PropertyViewModel
        {
            CustomerId = property.CustomerId,
            CustomerName = property.CustomerName,
            Address = property.Address1,
            City = property.City
        };

        return View(viewModel);
    }
}
```

**Benefits:**
- ✅ Works with any ERP (CompUFloor, Kudu, future providers)
- ✅ No SQL boilerplate
- ✅ Strongly-typed DTOs
- ✅ Automatic location/provider switching
- ✅ Easy to test (mock IErpDataClient)
- ✅ Cleaner code

---

## 🔄 Step-by-Step Refactoring Process

### **Step 1: Add Dependencies**

```csharp
// Add to constructor
private readonly ErpClientFactory _erpFactory;

public MyController(ErpClientFactory erpFactory, /* other dependencies */)
{
    _erpFactory = erpFactory;
    // ...
}
```

### **Step 2: Add GetErpContext Helper**

```csharp
/// <summary>
/// Helper to create ErpContext from current session location
/// </summary>
private ErpContext GetErpContext()
{
    var locationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
    return new ErpContext { LocationCode = locationCode };
}
```

### **Step 3: Replace SQL Queries**

**Find This Pattern:**
```csharp
var connStr = _configuration.GetConnectionString(locationCode);
using var conn = new SqlConnection(connStr);
await conn.OpenAsync();
var sql = "SELECT ... FROM CUSTOMER_MASTER WHERE ...";
using var cmd = new SqlCommand(sql, conn);
// ... parameters ...
using var reader = await cmd.ExecuteReaderAsync();
```

**Replace With:**
```csharp
var context = GetErpContext();
var client = _erpFactory.GetClient(context);
var result = await client.GetPropertyByIdAsync(propertyId, context);
```

### **Step 4: Map DTOs to ViewModels**

```csharp
// Old: Manual mapping from DataReader
var property = new PropertyViewModel
{
    CustomerId = reader.GetInt32(0),
    CustomerName = reader.GetString(1)
};

// New: Map from strongly-typed DTO
var property = await client.GetPropertyByIdAsync(propertyId, context);
var viewModel = new PropertyViewModel
{
    CustomerId = property.CustomerId,
    CustomerName = property.CustomerName,
    // DTO properties are named clearly!
};
```

---

## 📋 Controllers to Refactor (Priority Order)

### **🔴 High Priority (Most Used)**

1. **DashboardController.cs** (1504 lines)
   - Sales metrics queries
   - AR aging reports
   - Daily order counts
   - **Methods:** Index(), GetSalesMetrics(), GetARSummary()

2. **HomeController.cs** (1103 lines)
   - Dashboard metrics
   - Summary data
   - **Methods:** Index(), Dashboard()

3. **PropertiesController.cs** (819 lines)
   - Property lists
   - Property details
   - Invoice lists
   - **Methods:** Index(), Details(), GetInvoices()

### **🟡 Medium Priority**

4. **ManagementController.cs**
   - Properties by management company
   - **Methods:** Index(), GetByManagement()

5. **SalespersonController.cs**
   - Salesman performance
   - **Methods:** Index(), GetMetrics()

6. **OrdersController.cs**
   - Order details
   - Order line items
   - **Methods:** Details(), GetLineItems()

### **🟢 Low Priority (API-Specific)**

7. **PropertiesApiController.cs** ✅ Already uses ErpMergeService
8. **OrdersApiController.cs** ✅ Already uses ErpMergeService

---

## 🛠️ Service Refactoring

### **SalesMetricsService** (Critical!)

**Location:** `/Services/SalesMetricsService.cs`

**Current:** Complex SQL queries for sales metrics and caching

**Needs:**
- Extract SQL to `CompUFloorErpClient.GetSalesMetricsAsync()`
- Use abstraction layer
- Keep caching logic in service layer

**Pattern:**
```csharp
// Before
var connStr = _configuration.GetConnectionString(locationCode);
using var conn = new SqlConnection(connStr);
var sql = "SELECT COUNT(*) FROM AR_OPEN_ITEM...";
// ... complex query ...

// After
var context = new ErpContext { LocationCode = locationCode };
var client = _erpFactory.GetClient(context);
var metrics = await client.GetSalesMetricsAsync(startDate, endDate, context);
// Cache the result
_memoryCache.Set(cacheKey, metrics, TimeSpan.FromMinutes(5));
```

---

## 📝 Common Refactoring Patterns

### **Pattern 1: Single Property Lookup**

**Before:**
```csharp
var sql = "SELECT * FROM CUSTOMER_MASTER WHERE CUM_CUMMAS_ID = @id";
// ... SqlConnection, SqlCommand boilerplate ...
```

**After:**
```csharp
var property = await client.GetPropertyByIdAsync(customerId, context);
```

---

### **Pattern 2: Property Search**

**Before:**
```csharp
var sql = "SELECT TOP 20 * FROM CUSTOMER_MASTER WHERE CUM_CUSTOMER_NAME LIKE @term";
// ... SqlConnection, SqlCommand boilerplate ...
```

**After:**
```csharp
var results = await client.SearchPropertiesAsync(searchTerm, context, skip: 0, take: 20);
```

---

### **Pattern 3: Orders for Property**

**Before:**
```csharp
var sql = @"
    SELECT S.SOH_NUMBER, S.SOH_ORDER_DATE, S.SOH_TOTAL_AMOUNT
    FROM SALES_HEADER S
    WHERE S.SOH_CUMMAS_ID = @propertyId";
// ... SqlConnection, SqlCommand boilerplate ...
```

**After:**
```csharp
var orders = await client.GetOrdersForPropertyAsync(propertyId, context);
```

---

### **Pattern 4: Sales Metrics (Dashboard)**

**Before:**
```csharp
var sql = @"
    SELECT
        COUNT(DISTINCT ARO.ARO_INVOICE_NUMBER) AS TotalOrders,
        SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END) AS OnlineOrders,
        SUM(ARO.ARO_INVOICE_AMOUNT) AS TotalOrderAmount
    FROM AR_OPEN_ITEM ARO
    INNER JOIN INVOICE_HEADER IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
    WHERE ARO.ARO_INVOICE_TYPE = 'I'
      AND IHF.IHF_INVOICE_DATE BETWEEN @StartDate AND @EndDate";
// ... SqlConnection, SqlCommand boilerplate ...
```

**After:**
```csharp
var metrics = await client.GetSalesMetricsAsync(startDate, endDate, context);
// Returns ErpSalesMetrics with:
//   - TotalOrders
//   - OnlineOrders
//   - TotalOrderAmount
//   - AverageOrderValue
```

---

### **Pattern 5: AR Aging Report**

**Before:**
```csharp
var sql = @"
    SELECT
        COUNT(CASE WHEN DATEDIFF(day, ARO.ARO_DUE_DATE, GETDATE()) <= 30 THEN 1 END) as DueUnder30,
        SUM(CASE WHEN DATEDIFF(day, ARO.ARO_DUE_DATE, GETDATE()) <= 30 THEN ARO.ARO_INVOICE_BALANCE_DUE END) as DueUnder30Amount,
        -- ... more aging buckets ...
    FROM AR_OPEN_ITEM ARO";
// ... SqlConnection, SqlCommand boilerplate ...
```

**After:**
```csharp
var aging = await client.GetARAgingSummaryAsync(context);
// Returns ErpARAgingSummary with:
//   - DueUnder30, DueUnder30Amount
//   - Due30to60, Due30to60Amount
//   - Due60to90, Due60to90Amount
//   - DueOver120, DueOver120Amount
//   - TopDelinquentCustomers
```

---

## 🧪 Testing After Refactoring

### **Manual Testing Checklist**

1. ✅ Test with different locations (LAX, LSV, CHN, PHX, SND)
2. ✅ Verify data matches old implementation
3. ✅ Check performance (should be similar or better)
4. ✅ Test location switching (session changes)
5. ✅ Test with admin/GM users (multi-location access)

### **Automated Testing**

```csharp
[Fact]
public async Task GetProperty_ReturnsCorrectData()
{
    // Arrange
    var mockClient = new Mock<IErpDataClient>();
    mockClient.Setup(c => c.GetPropertyByIdAsync(123, It.IsAny<ErpContext>(), default))
        .ReturnsAsync(new ErpProperty
        {
            CustomerId = 123,
            CustomerName = "Test Property",
            City = "Los Angeles"
        });

    var factory = new Mock<ErpClientFactory>();
    factory.Setup(f => f.GetClient(It.IsAny<ErpContext>()))
        .Returns(mockClient.Object);

    var controller = new MyController(factory.Object, httpContextAccessor);

    // Act
    var result = await controller.GetProperty(123);

    // Assert
    var viewResult = Assert.IsType<ViewResult>(result);
    var model = Assert.IsType<PropertyViewModel>(viewResult.Model);
    Assert.Equal("Test Property", model.CustomerName);
}
```

---

## 🚀 Benefits Summary

After refactoring, your code will:

✅ **Work with any ERP** - Just change config, no code changes
✅ **Be testable** - Mock IErpDataClient in unit tests
✅ **Be maintainable** - SQL centralized in CompUFloorErpClient
✅ **Be type-safe** - Strongly-typed DTOs instead of DataTables
✅ **Be cleaner** - No SQL boilerplate in controllers
✅ **Support gradual migration** - Mix CompUFloor and Kudu during transition
✅ **Be provider-agnostic** - Same code works for all locations

---

## 📊 Progress Tracking

| Controller/Service | Lines | Status | Priority |
|-------------------|-------|--------|----------|
| ErpMergeService | 333 | 🟡 Partial | High |
| DashboardController | 1504 | ⏳ TODO | High |
| HomeController | 1103 | ⏳ TODO | High |
| PropertiesController | 819 | ⏳ TODO | High |
| ManagementController | - | ⏳ TODO | Medium |
| SalespersonController | - | ⏳ TODO | Medium |
| OrdersController | - | ⏳ TODO | Medium |
| SalesMetricsService | - | ⏳ TODO | High |

---

## 🎯 Next Steps

1. **Complete CompUFloorErpClient methods** (implement NotImplementedException methods)
2. **Refactor DashboardController** (highest priority - most used)
3. **Refactor SalesMetricsService** (critical for dashboard)
4. **Refactor HomeController** (second most used)
5. **Refactor PropertiesController** (third most used)
6. **Test thoroughly** after each refactoring
7. **Deploy incrementally** (one controller at a time)

---

**Last Updated:** 2026-01-05
**Status:** Phase 1 Complete - Ready for Continued Refactoring
