# Kudu Single-Database Configuration Guide

## 🏗️ Kudu Architecture (Based on Your Description)

```
┌─────────────────────────────────────────────────────────────┐
│  Kudu Pro Single Database                                   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Company-Wide (Shared) Data                          │  │
│  │  • Products (same across all stores)                 │  │
│  │  • Management Companies (company-wide)               │  │
│  │  • Price Codes (shared pricing)                      │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────┬──────────┬──────────┬──────────┬──────────┐  │
│  │ Store:   │ Store:   │ Store:   │ Store:   │ Store:   │  │
│  │  LAX     │  LSV     │  CHN     │  PHX     │  SND     │  │
│  │          │          │          │          │          │  │
│  │ Props    │ Props    │ Props    │ Props    │ Props    │  │
│  │ Orders   │ Orders   │ Orders   │ Orders   │ Orders   │  │
│  │ Invoices │ Invoices │ Invoices │ Invoices │ Invoices │  │
│  └──────────┴──────────┴──────────┴──────────┴──────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## ✅ Configuration When You Get Kudu

### **Option 1: All Locations on Kudu (Single Configuration)**

This is the **simplest** approach for Kudu's single-database model:

```json
{
  "ErpSettings": {
    "DefaultProvider": "CompUFloor",
    "Kudu": {
      "BaseUrl": "https://api.kudupro.com/v1",
      "ApiKey": "sk_live_abc123...",
      "CompanyId": "seamless-flooring",
      "Stores": ["LAX", "LSV", "CHN", "PHX", "SND"]
    },
    "Locations": {
      "LAX": {
        "LocationCode": "LAX",
        "Provider": "Kudu",
        "StoreId": "LAX"  // Maps to Kudu's store code
      },
      "LSV": {
        "LocationCode": "LSV",
        "Provider": "Kudu",
        "StoreId": "LSV"
      },
      "CHN": {
        "LocationCode": "CHN",
        "Provider": "Kudu",
        "StoreId": "CHN"
      },
      "PHX": {
        "LocationCode": "PHX",
        "Provider": "Kudu",
        "StoreId": "PHX"
      },
      "SND": {
        "LocationCode": "SND",
        "Provider": "Kudu",
        "StoreId": "SND"
      }
    }
  }
}
```

**How it works:**
- ✅ Single Kudu API connection for all stores
- ✅ Each API call includes `?storeId=LAX` parameter
- ✅ Products/Management Companies fetched once (company-wide)
- ✅ Properties/Orders filtered by store

---

### **Option 2: Gradual Migration (Mixed CompUFloor + Kudu)**

This is what you'll actually use during migration:

```json
{
  "ErpSettings": {
    "DefaultProvider": "CompUFloor",
    "Kudu": {
      "BaseUrl": "https://api.kudupro.com/v1",
      "ApiKey": "sk_live_abc123...",
      "CompanyId": "seamless-flooring"
    },
    "Locations": {
      "LAX": {
        "LocationCode": "LAX",
        "Provider": "CompUFloor",  // Still on CompUFloor
        "ConnectionString": "Data Source=...;Initial Catalog=CompUFloorLA;..."
      },
      "LSV": {
        "LocationCode": "LSV",
        "Provider": "CompUFloor",  // Still on CompUFloor
        "ConnectionString": "Data Source=...;Initial Catalog=CompUFloorLV;..."
      },
      "CHN": {
        "LocationCode": "CHN",
        "Provider": "CompUFloor",  // Still on CompUFloor
        "ConnectionString": "Data Source=...;Initial Catalog=CompUFloorChino;..."
      },
      "PHX": {
        "LocationCode": "PHX",
        "Provider": "Kudu",        // ✅ MIGRATED to Kudu!
        "StoreId": "PHX"
      },
      "SND": {
        "LocationCode": "SND",
        "Provider": "CompUFloor",  // Still on CompUFloor
        "ConnectionString": "Data Source=...;Initial Catalog=CompUFloorSD;..."
      }
    }
  }
}
```

**Result:**
- ✅ PHX data comes from Kudu API
- ✅ LAX, LSV, CHN, SND still use CompUFloor
- ✅ Users can switch between locations seamlessly
- ✅ No code changes needed!

---

## 🔄 How API Calls Work with Kudu's Single Database

### **Example 1: Get Properties for LAX**

**User Action:**
```csharp
// User switches to LAX location in UI
HttpContext.Session.SetString("LocationCode", "LAX");

// Controller calls ERP
var context = new ErpContext { LocationCode = "LAX" };
var client = _erpFactory.GetClient(context);
var properties = await client.GetAllPropertiesAsync(context);
```

**What Happens:**
```
1. ErpClientFactory sees LocationCode = "LAX"
2. Checks config: LAX.Provider = "Kudu"
3. Returns KuduErpClient
4. KuduErpClient makes API call:

   GET https://api.kudupro.com/v1/customers?storeId=LAX
   Headers:
     Authorization: Bearer sk_live_abc123...
     X-Company-Id: seamless-flooring

5. Kudu filters properties WHERE store_id = 'LAX'
6. Returns properties for LAX store only
```

---

### **Example 2: Get Products (Company-Wide Data)**

**User Action:**
```csharp
var context = new ErpContext { LocationCode = "LAX" };
var products = await client.GetProductsAsync(context);
```

**What Happens:**
```
GET https://api.kudupro.com/v1/products?companyId=seamless-flooring

Note: NO storeId filter! Products are company-wide.
Returns all products available to all stores.
```

---

### **Example 3: Get Management Companies (Company-Wide)**

```
GET https://api.kudupro.com/v1/management-companies?companyId=seamless-flooring

Returns all management companies (shared across stores).
```

---

### **Example 4: Get Orders for Property (Store-Specific)**

```csharp
var context = new ErpContext { LocationCode = "PHX" };
var orders = await client.GetOrdersForPropertyAsync(propertyId, context);
```

**API Call:**
```
GET https://api.kudupro.com/v1/customers/12345/orders?storeId=PHX

Kudu returns orders for this property at PHX store only.
```

---

## 🎯 Benefits of Kudu's Single-Database Model

### ✅ **Simpler Configuration**
- One API connection for all stores (not 5 separate connections)
- Shared credentials (single API key)
- No tenant ID per location

### ✅ **Better Performance**
- Company-wide data cached once (products, management companies)
- Reduced API calls for reference data
- Single authentication flow

### ✅ **Easier Data Consistency**
- Products guaranteed to be same across all stores
- Management companies centralized
- No sync issues between locations

### ✅ **Matches Your Current UX**
- Users already switch locations in SalesMetrics
- Admin/GM users already have multi-location access
- Same permission model

---

## 🔧 KuduErpClient Implementation Pattern

Here's how `KuduErpClient.cs` will work with single database:

```csharp
public class KuduErpClient : IErpDataClient
{
    private readonly HttpClient _httpClient;
    private readonly string _companyId = "seamless-flooring";

    public async Task<List<ErpProperty>> GetAllPropertiesAsync(
        ErpContext context,
        CancellationToken cancellationToken = default)
    {
        var storeId = context.LocationCode; // LAX, LSV, etc.

        // Add store filter to API call
        var response = await _httpClient.GetAsync(
            $"/api/v1/customers?companyId={_companyId}&storeId={storeId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ErpProperty>>();
    }

    public async Task<List<ErpProduct>> GetProductsAsync(
        ErpContext context,
        CancellationToken cancellationToken = default)
    {
        // NO store filter - products are company-wide
        var response = await _httpClient.GetAsync(
            $"/api/v1/products?companyId={_companyId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ErpProduct>>();
    }

    public async Task<List<ErpManagementCompany>> GetManagementCompaniesAsync(
        ErpContext context,
        CancellationToken cancellationToken = default)
    {
        // NO store filter - management companies are company-wide
        var response = await _httpClient.GetAsync(
            $"/api/v1/management-companies?companyId={_companyId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ErpManagementCompany>>();
    }
}
```

---

## 🚦 No Issues with This Architecture!

### **Your Question:**
> "Will that cause any issues with how the API is provided and connected to SalesMetrics?"

### **Answer: No Issues at All!** ✅

Here's why it works perfectly:

#### 1. **Location Switching Already Works**
Your SalesMetrics already has:
```csharp
// User switches location
HttpContext.Session.SetString("LocationCode", "LAX");

// Controllers use session location
var locationCode = HttpContext.Session.GetString("LocationCode");
```

This same pattern works with Kudu - just adds `?storeId=LAX` to API calls!

#### 2. **Company-Wide Data is Better**
```
Current (CompUFloor): Products might differ between databases
Future (Kudu): Products guaranteed consistent across stores ✅
```

#### 3. **Store-Specific Data Filtered Automatically**
```
var context = new ErpContext { LocationCode = "PHX" };
// KuduErpClient automatically adds ?storeId=PHX to API calls
```

#### 4. **Multi-Location Access (GMs/Admins) Works**
```csharp
// Admin views all properties across all stores
var allLocations = new[] { "LAX", "LSV", "CHN", "PHX", "SND" };
var allProperties = new List<ErpProperty>();

foreach (var location in allLocations)
{
    var context = new ErpContext { LocationCode = location };
    var client = _erpFactory.GetClient(context);
    var properties = await client.GetAllPropertiesAsync(context);
    allProperties.AddRange(properties);
}

// Now admin sees properties from all 5 stores
```

---

## 📊 Comparison: CompUFloor vs Kudu

| Feature | CompUFloor (Current) | Kudu (Future) |
|---------|---------------------|---------------|
| **Database Structure** | 5 separate databases | 1 database, 5 stores |
| **API Calls** | N/A (direct SQL) | REST API with store filter |
| **Products** | Per database (might differ) | Company-wide (consistent) |
| **Management Companies** | Per database | Company-wide (consistent) |
| **Properties** | Per database | Per store (filtered by API) |
| **Location Switching** | Change connection string | Change `?storeId=XXX` param |
| **Admin Multi-Access** | Loop through 5 DBs | Loop through 5 stores (same DB) |
| **Configuration** | 5 connection strings | 1 API URL + store codes |

---

## ✅ Final Configuration When Fully Migrated

Once all 5 locations are on Kudu:

```json
{
  "ErpSettings": {
    "DefaultProvider": "Kudu",
    "Kudu": {
      "BaseUrl": "https://api.kudupro.com/v1",
      "ApiKey": "sk_live_...",
      "CompanyId": "seamless-flooring",
      "TimeoutSeconds": 30
    },
    "Locations": {
      "LAX": { "LocationCode": "LAX", "Provider": "Kudu", "StoreId": "LAX" },
      "LSV": { "LocationCode": "LSV", "Provider": "Kudu", "StoreId": "LSV" },
      "CHN": { "LocationCode": "CHN", "Provider": "Kudu", "StoreId": "CHN" },
      "PHX": { "LocationCode": "PHX", "Provider": "Kudu", "StoreId": "PHX" },
      "SND": { "LocationCode": "SND", "Provider": "Kudu", "StoreId": "SND" }
    }
  }
}
```

**That's it!** Simple, clean, and works seamlessly.

---

## 🎯 Summary

Kudu's single-database-with-stores architecture is **ideal** for your abstraction layer:

✅ **No Issues** - works perfectly with ErpContext + LocationCode pattern
✅ **Simpler** - single API connection instead of 5 databases
✅ **Consistent** - company-wide data guaranteed same across stores
✅ **Flexible** - can migrate stores one at a time
✅ **Familiar** - matches how users already work in SalesMetrics

The abstraction layer is **ready** for this architecture!

---

**Last Updated:** 2026-01-05
**Status:** Ready for Kudu single-database model
