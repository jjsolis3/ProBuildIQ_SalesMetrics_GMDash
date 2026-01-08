# SalesMetrics ERP Migration Guide

## 🎯 Overview

This guide documents the migration from direct SQL connections to CompUFloor databases to a flexible, API-based abstraction layer that supports multiple ERP providers (CompUFloor, Kudu, and future systems).

## ✅ What Has Been Implemented (Phase 1)

### 1. **ERP Abstraction Layer**

#### New Files Created:
```
Services/Erp/
├── Models/
│   ├── ErpProperty.cs          # Property/Customer DTOs
│   ├── ErpOrder.cs              # Order DTOs
│   ├── ErpInvoice.cs            # Invoice DTOs
│   ├── ErpSalesMetrics.cs       # Sales metrics DTOs
│   └── ErpReferenceData.cs      # Reference data DTOs (Salesman, Warehouse, etc.)
├── Configuration/
│   └── ErpSettings.cs           # Multi-provider configuration models
├── Clients/
│   ├── CompUFloorErpClient.cs   # CompUFloor SQL implementation
│   └── KuduErpClient.cs         # Kudu API stub implementation
├── IErpDataClient.cs            # Enhanced interface with domain methods
└── ErpClientFactory.cs          # Factory for provider selection
```

### 2. **Domain-Driven Interface (`IErpDataClient`)**

The new interface provides domain-specific methods instead of raw SQL queries:

**Properties/Customers:**
- `SearchPropertiesAsync()` - Search properties by name
- `GetPropertyByIdAsync()` - Get single property details
- `GetPropertyDetailsAsync()` - Get property with AR and sales data
- `GetAllPropertiesAsync()` - Get all properties for a location
- `GetPropertiesByManagementAsync()` - Group by management company

**Orders:**
- `GetOrdersForPropertyAsync()` - Get orders for a property
- `GetOrderDetailAsync()` - Get order with line items
- `GetOrdersByDateRangeAsync()` - Get orders by date range

**Invoices & AR:**
- `GetInvoicesForPropertyAsync()` - Get invoices for a property
- `GetInvoiceDetailAsync()` - Get invoice details
- `GetPropertyARBalanceAsync()` - Get AR balance
- `GetARAgingSummaryAsync()` - Get AR aging report
- `GetOverdueInvoicesAsync()` - Get overdue invoices
- `GetMonthlyInvoiceSummariesAsync()` - Monthly summaries

**Sales Metrics:**
- `GetSalesMetricsAsync()` - Aggregated sales metrics
- `GetDailyOrderCountsAsync()` - Daily order counts
- `GetSalesmanMetricsAsync()` - Salesman performance

**Reference Data:**
- `GetSalesmenAsync()` - Get salesmen/reps
- `GetPriceCodesAsync()` - Get price codes
- `GetWarehousesAsync()` - Get warehouses

### 3. **Multi-Provider Configuration**

#### `appsettings.json` - New Section:
```json
"ErpSettings": {
  "DefaultProvider": "CompUFloor",
  "WarehouseMappings": {
    "LAX": [ 1, 3 ],
    "LSV": [ 1, 2, 3, 6 ],
    "CHN": [ 1 ],
    "PHX": [ 1, 3 ],
    "SND": [ 1 ]
  },
  "Locations": {
    "LAX": {
      "LocationCode": "LAX",
      "Provider": "CompUFloor",  // <-- Can switch to "Kudu"
      "ConnectionString": "..."
    },
    "LSV": { ... },
    "CHN": { ... },
    "PHX": { ... },
    "SND": { ... }
  }
}
```

### 4. **Dependency Injection Setup**

#### `Program.cs` Updates:
```csharp
// Configure ERP settings
builder.Services.Configure<ErpSettings>(
    builder.Configuration.GetSection("ErpSettings"));

// Register ERP client implementations
builder.Services.AddSingleton<CompUFloorErpClient>();
builder.Services.AddSingleton<KuduErpClient>();
builder.Services.AddHttpClient<KuduErpClient>();

// Register factory for provider selection
builder.Services.AddSingleton<ErpClientFactory>();

// Default IErpDataClient (backward compatible)
builder.Services.AddSingleton<IErpDataClient>(sp =>
    sp.GetRequiredService<CompUFloorErpClient>());
```

---

## 🔄 How to Migrate a Location to Kudu

### Step 1: Update Configuration

Change the provider for a location in `appsettings.json`:

```json
"PHX": {
  "LocationCode": "PHX",
  "Provider": "Kudu",  // Changed from "CompUFloor"
  "ApiSettings": {
    "BaseUrl": "https://api.kudupro.com/v1",
    "ApiKey": "your-api-key-here",
    "TenantId": "seamless-phx",
    "TimeoutSeconds": 30,
    "RetryAttempts": 3
  }
}
```

### Step 2: Implement Kudu Methods

Update `/Services/Erp/Clients/KuduErpClient.cs` with actual Kudu API calls:

```csharp
public async Task<ErpProperty?> GetPropertyByIdAsync(
    int customerId,
    ErpContext context,
    CancellationToken cancellationToken = default)
{
    var apiSettings = GetApiSettings(context);
    ConfigureHttpClient(apiSettings);

    // Replace with actual Kudu endpoint
    var response = await _httpClient.GetAsync(
        $"/api/v1/customers/{customerId}",
        cancellationToken);

    return await response.Content.ReadFromJsonAsync<ErpProperty>();
}
```

### Step 3: Test the Migration

1. Update code to use ErpClientFactory:
   ```csharp
   // In a controller
   private readonly ErpClientFactory _erpFactory;

   public async Task<IActionResult> Index()
   {
       var context = new ErpContext { LocationCode = "PHX" };
       var client = _erpFactory.GetClient(context);

       // This will use KuduErpClient for PHX!
       var properties = await client.GetAllPropertiesAsync(context);

       return View(properties);
   }
   ```

2. Test that PHX data comes from Kudu while other locations use CompUFloor

---

## 📋 Next Steps (Phase 2)

### 1. **Complete CompUFloorErpClient Implementation**

Currently implemented (partial):
- ✅ SearchPropertiesAsync
- ✅ GetPropertyByIdAsync
- ✅ GetAllPropertiesAsync
- ✅ GetOrdersForPropertyAsync
- ✅ GetSalesmenAsync
- ✅ GetPriceCodesAsync
- ✅ GetWarehousesAsync

Still needed (marked as `NotImplementedException`):
- ❌ GetPropertyDetailsAsync
- ❌ GetPropertiesByManagementAsync
- ❌ GetOrderDetailAsync
- ❌ GetOrdersByDateRangeAsync
- ❌ GetInvoicesForPropertyAsync
- ❌ GetInvoiceDetailAsync
- ❌ GetPropertyARBalanceAsync
- ❌ GetARAgingSummaryAsync
- ❌ GetOverdueInvoicesAsync
- ❌ GetMonthlyInvoiceSummariesAsync
- ❌ GetSalesMetricsAsync (critical!)
- ❌ GetDailyOrderCountsAsync
- ❌ GetSalesmanMetricsAsync

**Action:** Extract SQL queries from existing controllers/services and move them into CompUFloorErpClient methods.

### 2. **Refactor Existing Services**

**Priority Services to Refactor:**
1. `SalesMetricsService.cs` (lines 70-150) - Move sales metrics queries
2. `ErpMergeService.cs` (lines 30-90) - Move property/order lookup
3. `DashboardController.cs` (lines 100-500) - Use IErpDataClient
4. `HomeController.cs` (lines 50-300) - Use IErpDataClient
5. `PropertiesController.cs` (lines 40-250) - Use IErpDataClient

**Example Refactoring Pattern:**

**Before (Direct SQL):**
```csharp
public class DashboardController : Controller
{
    private readonly IConfiguration _configuration;

    public async Task<IActionResult> Index()
    {
        var connString = _configuration.GetConnectionString("LAX");
        using var connection = new SqlConnection(connString);

        var sql = "SELECT * FROM CUSTOMER_MASTER WHERE...";
        var properties = await connection.QueryAsync<Property>(sql);

        return View(properties);
    }
}
```

**After (Using Abstraction):**
```csharp
public class DashboardController : Controller
{
    private readonly ErpClientFactory _erpFactory;

    public async Task<IActionResult> Index()
    {
        var context = new ErpContext
        {
            LocationCode = HttpContext.Session.GetString("LocationCode")
        };

        var client = _erpFactory.GetClient(context);
        var properties = await client.GetAllPropertiesAsync(context);

        return View(properties);
    }
}
```

### 3. **Research Kudu API**

**Action Items:**
1. Contact Kudu Pro support for API documentation
2. Request sandbox/test environment access
3. Document endpoint mappings:
   ```
   CompUFloor Table          → Kudu API Endpoint
   ────────────────────────────────────────────
   CUSTOMER_MASTER           → GET /api/v1/customers
   SALES_HEADER              → GET /api/v1/orders
   AR_OPEN_ITEM              → GET /api/v1/invoices
   SALESMAN_MASTER           → GET /api/v1/salespeople
   ```

4. Implement authentication (OAuth, API keys, etc.)
5. Map Kudu data models to our DTO models

### 4. **Testing Strategy**

**Unit Tests:**
```csharp
[Fact]
public async Task GetPropertyById_CompUFloor_ReturnsProperty()
{
    // Arrange
    var client = new CompUFloorErpClient(...);
    var context = new ErpContext { LocationCode = "LAX" };

    // Act
    var property = await client.GetPropertyByIdAsync(123, context);

    // Assert
    Assert.NotNull(property);
    Assert.Equal("Property Name", property.CustomerName);
}
```

**Integration Tests:**
- Test provider switching (CompUFloor → Kudu)
- Test data consistency between providers
- Test fallback behavior if API fails

### 5. **Gradual Rollout Plan**

**Recommended Migration Order:**
1. **Phase 2a**: PHX location (smallest, easiest to test)
2. **Phase 2b**: SND location
3. **Phase 2c**: CHN location
4. **Phase 2d**: LAX location
5. **Phase 2e**: LSV location (largest, most complex)

**Per-Location Checklist:**
- [ ] Kudu API credentials obtained
- [ ] Configuration updated in `appsettings.json`
- [ ] Kudu client methods implemented for this location
- [ ] Integration testing completed
- [ ] User acceptance testing completed
- [ ] Monitor for 1 week, then proceed to next location

---

## 🛠️ Configuration Reference

### Complete ErpSettings Example

```json
{
  "ErpSettings": {
    "DefaultProvider": "CompUFloor",
    "WarehouseMappings": {
      "LAX": [ 1, 3 ],
      "LSV": [ 1, 2, 3, 6 ],
      "CHN": [ 1 ],
      "PHX": [ 1, 3 ],
      "SND": [ 1 ]
    },
    "Locations": {
      "LAX": {
        "LocationCode": "LAX",
        "Provider": "CompUFloor",
        "ConnectionString": "Data Source=...;Initial Catalog=CompUFloorLA;..."
      },
      "PHX": {
        "LocationCode": "PHX",
        "Provider": "Kudu",
        "ApiSettings": {
          "BaseUrl": "https://api.kudupro.com/v1",
          "ApiKey": "sk_live_...",
          "TenantId": "seamless-phx",
          "TimeoutSeconds": 30,
          "RetryAttempts": 3,
          "OAuth": {
            "Authority": "https://auth.kudupro.com",
            "ClientId": "seamless-client-id",
            "ClientSecret": "your-client-secret",
            "Scope": "read:customers read:orders"
          }
        }
      }
    }
  }
}
```

### ErpClientFactory Usage

```csharp
// Inject factory
private readonly ErpClientFactory _erpFactory;

// Get client for specific location
var context = new ErpContext { LocationCode = "LAX" };
var client = _erpFactory.GetClient(context);

// Or from LocationId
var context = new ErpContext { LocationId = 1 }; // 1 = LAX
var client = _erpFactory.GetClient(context);

// Check provider type
var provider = _erpFactory.GetProviderType("LAX"); // Returns "CompUFloor"
var usesKudu = _erpFactory.UsesProvider("PHX", "Kudu"); // Returns true if PHX uses Kudu

// Use the client
var properties = await client.GetAllPropertiesAsync(context);
```

---

## 🔍 Troubleshooting

### Issue: "NotImplementedException" errors

**Cause:** Method not yet implemented in CompUFloorErpClient
**Solution:** Implement the method by extracting SQL from existing code

### Issue: Provider not switching

**Check:**
1. ErpSettings configuration is correct in `appsettings.json`
2. Services are registered in `Program.cs`
3. ErpClientFactory is being used (not direct IErpDataClient injection)

### Issue: Kudu API authentication fails

**Check:**
1. API key is valid and not expired
2. Tenant ID is correct
3. Base URL is reachable
4. OAuth scopes match required permissions

---

## 📊 Benefits of This Architecture

### ✅ Multi-Provider Support
- Switch ERPs per location without code changes
- Support CompUFloor and Kudu simultaneously during migration
- Easy to add new ERPs in the future

### ✅ Cleaner Code
- Domain-driven methods instead of raw SQL
- Strongly-typed DTOs instead of DataTables
- Centralized data access logic

### ✅ Testability
- Mock IErpDataClient for unit tests
- Test provider switching independently
- Integration tests for each provider

### ✅ Multi-Customer Ready
- Each customer can have different ERP
- Configuration-driven setup
- No code changes to onboard new customers

### ✅ Maintainability
- SQL queries in one place (CompUFloorErpClient)
- Clear separation of concerns
- Easy to debug and optimize

---

## 📝 Notes

- **Backward Compatibility:** Old `QueryAsync()` method kept for transition
- **Gradual Migration:** Can migrate one controller/service at a time
- **No Breaking Changes:** Existing code continues to work during migration
- **Future-Proof:** Ready for any ERP vendor (JobNimbus, Procore, etc.)

---

## 🚀 Quick Start

### For Developers: How to Use the New System

1. **Inject ErpClientFactory:**
   ```csharp
   private readonly ErpClientFactory _erpFactory;

   public MyController(ErpClientFactory erpFactory)
   {
       _erpFactory = erpFactory;
   }
   ```

2. **Get the appropriate client:**
   ```csharp
   var context = new ErpContext
   {
       LocationCode = HttpContext.Session.GetString("LocationCode")
   };
   var client = _erpFactory.GetClient(context);
   ```

3. **Call domain methods:**
   ```csharp
   // Instead of raw SQL
   var properties = await client.SearchPropertiesAsync("Sunset", context, skip: 0, take: 20);
   var orders = await client.GetOrdersForPropertyAsync(123, context);
   var metrics = await client.GetSalesMetricsAsync(startDate, endDate, context);
   ```

4. **Map to ViewModels if needed:**
   ```csharp
   var viewModel = properties.Items.Select(p => new PropertyViewModel
   {
       CustomerId = p.CustomerId,
       CustomerName = p.CustomerName,
       ARBalance = p.ARBalance ?? 0
   }).ToList();
   ```

---

## Contact & Support

For questions about this migration:
- Review this guide
- Check `/Services/Erp/` for implementation details
- Refer to existing implementations in `CompUFloorErpClient.cs`

---

**Last Updated:** 2026-01-05
**Version:** 1.0 (Phase 1 Complete)
