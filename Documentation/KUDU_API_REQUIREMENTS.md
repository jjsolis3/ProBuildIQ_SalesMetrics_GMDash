# Kudu Pro API Requirements for SalesMetrics Integration

## 📋 Executive Summary

This document specifies the **exact data requirements** for integrating SalesMetrics with Kudu Pro Flooring Software. It serves as a requirements specification that can be shared with Kudu support to request API access.

**Current State:** SalesMetrics connects to 5 CompUFloor SQL Server databases (LAX, LSV, CHN, PHX, SND)

**Goal:** Migrate to Kudu Pro API while maintaining access to CompUFloor historical data

**Approach:** Dual-provider support (CompUFloor for historical, Kudu for current)

---

## 🎯 Required API Endpoints

Below is a complete specification of the API endpoints Kudu Pro needs to provide for SalesMetrics integration.

---

## 1️⃣ **CUSTOMERS / PROPERTIES**

### Endpoint: `GET /api/v1/customers/search`

**Purpose:** Search for properties/customers by name or customer number

**CompUFloor Equivalent:**
```sql
-- Current CompUFloor query
SELECT
    CUM_CUMMAS_ID as CustomerId,
    CUM_CUSTOMER_NUMBER as CustomerNumber,
    CUM_CUSTOMER_NAME as CustomerName
FROM CUSTOMER_MASTER C
WHERE C.CUM_CUSTOMER_NAME LIKE '%search_term%'
ORDER BY C.CUM_CUSTOMER_NAME
```

**Required Parameters:**
- `q` (string, required): Search term
- `skip` (int, optional, default: 0): Pagination offset
- `take` (int, optional, default: 20): Number of results

**Required Response:**
```json
{
  "items": [
    {
      "customerId": 12345,
      "customerNumber": "10001",
      "customerName": "Sunset Village Apartments"
    }
  ],
  "totalCount": 150,
  "skip": 0,
  "take": 20
}
```

**Data Points Required:**
- ✅ Customer ID (unique identifier)
- ✅ Customer Number (display reference)
- ✅ Customer Name (searchable)

---

### Endpoint: `GET /api/v1/customers/{customerId}`

**Purpose:** Get detailed customer/property information

**CompUFloor Equivalent:**
```sql
SELECT
    C.CUM_CUMMAS_ID as CustomerId,
    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
    C.CUM_CUSTOMER_NAME as CustomerName,
    C.CUM_ADDRESS_1 as Address1,
    C.CUM_ADDRESS_2 as Address2,
    C.CUM_ADDRESS_3 as Address3,
    C.CUM_CITY as City,
    C.CUM_STATE as State,
    C.CUM_ZIP as Zip,
    C.CUM_PHONE_NUMBER as PhoneNumber,
    C.CUM_EMAIL as Email,
    C.CUM_CREDIT_LIMIT as CreditLimit,
    C.CUM_AR_BALANCE as ARBalance,
    C.CUM_CREDIT_HOLD_FLAG as CreditHoldFlag,
    C.CUM_PRICE_CODE as PriceCode,
    C.CUM_SMNMAS_ID as SalesmanId,
    C.CUM_DATE_ESTABLISHED as EstablishedDate,
    C.CUM_ATTENTION_TO as AttentionTo,
    C.CUM_PO_NUMBER_REQUIRED as PONumberRequired,
    S.SMN_SALESMAN_NAME as SalesmanName
FROM CUSTOMER_MASTER C
LEFT JOIN SALESMAN_MASTER S ON C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID
WHERE C.CUM_CUMMAS_ID = @CustomerId
```

**Required Response:**
```json
{
  "customerId": 12345,
  "customerNumber": "10001",
  "customerName": "Sunset Village Apartments",
  "address": {
    "line1": "123 Main Street",
    "line2": "Building A",
    "line3": null,
    "city": "Los Angeles",
    "state": "CA",
    "zip": "90001"
  },
  "contact": {
    "phone": "(555) 123-4567",
    "email": "manager@sunsetvillage.com",
    "attentionTo": "John Smith, Property Manager"
  },
  "financial": {
    "creditLimit": 50000.00,
    "currentARBalance": 12500.50,
    "creditHoldFlag": false
  },
  "settings": {
    "priceCode": 5,
    "priceCodeDescription": "Standard Pricing",
    "poNumberRequired": true,
    "establishedDate": "2020-01-15T00:00:00Z"
  },
  "salesman": {
    "salesmanId": 42,
    "salesmanName": "Jane Doe",
    "salesmanNumber": "SD-001"
  },
  "managementCompany": "ABC Property Management"
}
```

**Data Points Required:**
- ✅ Basic Info: ID, Number, Name
- ✅ Address: Full address (line1, line2, line3, city, state, zip)
- ✅ Contact: Phone, Email, Attention To
- ✅ Financial: Credit Limit, AR Balance, Credit Hold Status
- ✅ Settings: Price Code, PO Required, Established Date
- ✅ Salesman: ID, Name, Number
- ✅ Management Company Name

---

### Endpoint: `GET /api/v1/customers`

**Purpose:** Get all customers for a location/branch

**CompUFloor Equivalent:**
```sql
SELECT
    C.CUM_CUMMAS_ID as CustomerId,
    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
    C.CUM_CUSTOMER_NAME as CustomerName,
    C.CUM_ADDRESS_1 as Address1,
    C.CUM_CITY as City,
    C.CUM_STATE as State,
    C.CUM_ZIP as Zip,
    C.CUM_CREDIT_LIMIT as CreditLimit,
    C.CUM_AR_BALANCE as ARBalance,
    C.CUM_CREDIT_HOLD_FLAG as CreditHoldFlag,
    C.CUM_PRICE_CODE as PriceCode,
    S.SMN_SALESMAN_NAME as SalesmanName
FROM CUSTOMER_MASTER C
LEFT JOIN SALESMAN_MASTER S ON C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID
ORDER BY C.CUM_CUSTOMER_NAME
```

**Required Parameters:**
- `branchCode` (string, optional): Filter by branch (LAX, LSV, CHN, PHX, SND)
- `includeInactive` (bool, optional, default: false): Include inactive customers

**Required Response:**
```json
{
  "items": [
    {
      "customerId": 12345,
      "customerNumber": "10001",
      "customerName": "Sunset Village Apartments",
      "city": "Los Angeles",
      "state": "CA",
      "creditLimit": 50000.00,
      "arBalance": 12500.50,
      "creditHold": false,
      "salesmanName": "Jane Doe",
      "priceCode": 5
    }
  ],
  "totalCount": 1250
}
```

---

## 2️⃣ **SALES ORDERS**

### Endpoint: `GET /api/v1/customers/{customerId}/orders`

**Purpose:** Get all orders for a specific customer

**CompUFloor Equivalent:**
```sql
SELECT
    S.SOH_SALOHD_ID as OrderId,
    S.SOH_NUMBER as OrderNumber,
    S.SOH_CUMMAS_ID as CustomerId,
    C.CUM_CUSTOMER_NAME as CustomerName,
    S.SOH_ORDER_DATE as OrderDate,
    S.SOH_DELIVERY_DATE as DeliveryDate,
    S.SOH_TOTAL_AMOUNT as TotalAmount,
    S.SOH_WHSMAS_ID as WarehouseId,
    S.SOH_OPERATOR as Operator,
    CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END as IsOnlineOrder,
    S.SOH_CANCELED_DATE as CanceledDate,
    S.SOH_SMNMAS_ID as SalesmanId,
    SM.SMN_SALESMAN_NAME as SalesmanName,
    S.SOH_CUSTOMER_PO as CustomerPO,
    S.SOH_SHIP_ADDRESS as ShipAddress,
    S.SOH_SHIP_CITY as ShipCity,
    S.SOH_SHIP_STATE as ShipState,
    S.SOH_SHIP_ZIP as ShipZip
FROM SALES_HEADER S
INNER JOIN CUSTOMER_MASTER C ON S.SOH_CUMMAS_ID = C.CUM_CUMMAS_ID
LEFT JOIN SALESMAN_MASTER SM ON S.SOH_SMNMAS_ID = SM.SMN_SMNMAS_ID
WHERE S.SOH_CUMMAS_ID = @CustomerId
    AND (@StartDate IS NULL OR S.SOH_ORDER_DATE >= @StartDate)
    AND (@EndDate IS NULL OR S.SOH_ORDER_DATE <= @EndDate)
ORDER BY S.SOH_ORDER_DATE DESC
```

**Required Parameters:**
- `startDate` (datetime, optional): Filter orders from this date
- `endDate` (datetime, optional): Filter orders to this date
- `skip` (int, optional): Pagination
- `take` (int, optional): Page size

**Required Response:**
```json
{
  "items": [
    {
      "orderId": 98765,
      "orderNumber": "SO-2024-0001",
      "customerId": 12345,
      "customerName": "Sunset Village Apartments",
      "orderDate": "2024-01-15T10:30:00Z",
      "deliveryDate": "2024-01-20T00:00:00Z",
      "totalAmount": 5432.10,
      "warehouseId": 1,
      "warehouseName": "Los Angeles Warehouse",
      "orderType": "Online",
      "isOnlineOrder": true,
      "isCanceled": false,
      "canceledDate": null,
      "salesman": {
        "salesmanId": 42,
        "salesmanName": "Jane Doe"
      },
      "customerPO": "PO-2024-ABC",
      "shippingAddress": {
        "address": "123 Main Street",
        "city": "Los Angeles",
        "state": "CA",
        "zip": "90001"
      }
    }
  ],
  "totalCount": 250,
  "skip": 0,
  "take": 50
}
```

**Data Points Required:**
- ✅ Order Info: ID, Number, Date, Delivery Date, Total Amount
- ✅ Customer: ID, Name
- ✅ Warehouse: ID, Name
- ✅ Order Type: Online flag, Operator
- ✅ Status: Canceled flag, Canceled date
- ✅ Salesman: ID, Name
- ✅ Customer PO
- ✅ Shipping Address

---

### Endpoint: `GET /api/v1/orders/{orderId}`

**Purpose:** Get detailed order information with line items

**CompUFloor Equivalent:**
```sql
-- Order Header
SELECT
    S.SOH_NUMBER as OrderNumber,
    C.CUM_CUSTOMER_NAME as CustomerName,
    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
    S.SOH_ORDER_DATE as OrderDate,
    S.SOH_DELIVERY_DATE as DeliveryDate,
    SM.SMN_SALESMAN_NAME as Salesperson,
    S.SOH_CUSTOMER_PO as CustomerPO,
    S.SOH_SHIP_ADDRESS as ShipAddress,
    S.SOH_SHIP_STATE as ShipState,
    S.SOH_SHIP_ZIP as ShipZip,
    S.SOH_BUILDING as Building,
    S.SOH_APT_NUMBER as AptNumber,
    PC.IPC_DESCRIPTION as MgmtCo
FROM SALES_HEADER S
LEFT JOIN CUSTOMER_MASTER C ON S.SOH_CUMMAS_ID = C.CUM_CUMMAS_ID
LEFT JOIN SALESMAN_MASTER SM ON S.SOH_SMNMAS_ID = SM.SMN_SMNMAS_ID
LEFT JOIN PRICE_CODES PC ON C.CUM_PRICE_CODE = PC.IPC_PRICE_CODE
WHERE S.SOH_SALOHD_ID = @OrderId

-- Line Items
SELECT
    I.ITM_ID_FIRST as ProductStyle,
    I.ITM_ID_LAST as ProductColor,
    D.SDT_ORDER_QTY_ORDER as Quantity,
    D.SDT_UOM as UOM,
    P.PCL_DESCRIPTION as ProductClass,
    I.ITM_DESCRIPTION as Description,
    D.SDT_EXTENDED_PRICE as ProductTotal
FROM SALES_DETAIL D
LEFT JOIN ITEM_MASTER I ON D.SDT_ITMMAS_ID = I.ITM_ITMMAS_ID
LEFT JOIN PRODUCT_CLASS P ON I.ITM_PCLMAS_ID = P.PCL_PCLMAS_ID
WHERE D.SDT_SALOHD_ID = @OrderId
```

**Required Response:**
```json
{
  "orderId": 98765,
  "orderNumber": "SO-2024-0001",
  "customer": {
    "customerId": 12345,
    "customerNumber": "10001",
    "customerName": "Sunset Village Apartments"
  },
  "dates": {
    "orderDate": "2024-01-15T10:30:00Z",
    "deliveryDate": "2024-01-20T00:00:00Z",
    "invoiceDate": "2024-01-21T00:00:00Z",
    "paidInFullDate": null
  },
  "salesman": {
    "salesmanId": 42,
    "salesmanName": "Jane Doe"
  },
  "customerPO": "PO-2024-ABC",
  "shippingAddress": {
    "address": "123 Main Street",
    "city": "Los Angeles",
    "state": "CA",
    "zip": "90001",
    "building": "Building A",
    "aptNumber": "Unit 205"
  },
  "managementCompany": "ABC Property Management",
  "lineItems": [
    {
      "lineId": 1,
      "productStyle": "CARPET-001",
      "productColor": "Beige",
      "quantity": 500.00,
      "uom": "SY",
      "productClass": "Carpet",
      "description": "Premium Berber Carpet",
      "productTotal": 2500.00
    },
    {
      "lineId": 2,
      "productStyle": "PAD-001",
      "productColor": null,
      "quantity": 500.00,
      "uom": "SY",
      "productClass": "Padding",
      "description": "6lb Carpet Pad",
      "productTotal": 750.00
    }
  ],
  "notes": [
    {
      "lineNumber": 1,
      "comment": "Install in hallways and common areas"
    }
  ],
  "totals": {
    "subtotal": 3250.00,
    "tax": 0.00,
    "total": 3250.00,
    "balance": 3250.00
  }
}
```

**Data Points Required:**
- ✅ Order Header: All fields from previous endpoint
- ✅ Line Items: Product info, quantities, pricing
- ✅ Notes/Comments per line
- ✅ Totals: Subtotal, Tax, Total, Balance

---

### Endpoint: `GET /api/v1/orders`

**Purpose:** Get orders by date range (for dashboard metrics)

**CompUFloor Equivalent:**
```sql
SELECT
    S.SOH_SALOHD_ID as OrderId,
    S.SOH_NUMBER as OrderNumber,
    S.SOH_ORDER_DATE as OrderDate,
    S.SOH_TOTAL_AMOUNT as TotalAmount,
    S.SOH_OPERATOR as Operator,
    S.SOH_SMNMAS_ID as SalesmanId
FROM SALES_HEADER S
WHERE S.SOH_ORDER_DATE BETWEEN @StartDate AND @EndDate
    AND S.SOH_WHSMAS_ID IN (@WarehouseIds)
    AND (@SalesmanId IS NULL OR S.SOH_SMNMAS_ID = @SalesmanId)
```

**Required Parameters:**
- `startDate` (datetime, required): Start date
- `endDate` (datetime, required): End date
- `branchCode` (string, optional): Filter by branch
- `salesmanId` (int, optional): Filter by salesman
- `warehouseIds` (int[], optional): Filter by warehouse IDs

**Required Response:**
```json
{
  "items": [
    {
      "orderId": 98765,
      "orderNumber": "SO-2024-0001",
      "orderDate": "2024-01-15T10:30:00Z",
      "totalAmount": 5432.10,
      "orderType": "Online",
      "salesmanId": 42
    }
  ],
  "summary": {
    "totalOrders": 150,
    "totalAmount": 125000.50,
    "onlineOrders": 75,
    "inStoreOrders": 75
  }
}
```

---

## 3️⃣ **INVOICES & ACCOUNTS RECEIVABLE**

### Endpoint: `GET /api/v1/customers/{customerId}/invoices`

**Purpose:** Get invoices for a customer

**CompUFloor Equivalent:**
```sql
SELECT
    ARO.ARO_INVOICE_NUMBER as InvoiceNumber,
    IHF.IHF_ORDER_NUMBER as OrderNumber,
    C.CUM_CUMMAS_ID as CustomerId,
    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
    C.CUM_CUSTOMER_NAME as CustomerName,
    IHF.IHF_INVOICE_DATE as InvoiceDate,
    ARO.ARO_INVOICE_AMOUNT as InvoiceAmount,
    ARO.ARO_INVOICE_BALANCE_DUE as BalanceDue,
    ARO.ARO_DUE_DATE as DueDate,
    ARO.ARO_DATE_PAID_IN_FULL as PaidInFullDate,
    ARO.ARO_INVOICE_TYPE as InvoiceType
FROM AR_OPEN_ITEM ARO
INNER JOIN INVOICE_HEADER IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
INNER JOIN CUSTOMER_MASTER C ON ARO.ARO_CUMMAS_ID = C.CUM_CUMMAS_ID
WHERE ARO.ARO_CUMMAS_ID = @CustomerId
    AND ARO.ARO_INVOICE_TYPE = 'I'
    AND (@StartDate IS NULL OR IHF.IHF_INVOICE_DATE >= @StartDate)
    AND (@EndDate IS NULL OR IHF.IHF_INVOICE_DATE <= @EndDate)
```

**Required Response:**
```json
{
  "items": [
    {
      "invoiceNumber": "INV-2024-0001",
      "orderNumber": "SO-2024-0001",
      "customerId": 12345,
      "customerNumber": "10001",
      "customerName": "Sunset Village Apartments",
      "invoiceDate": "2024-01-21T00:00:00Z",
      "invoiceAmount": 5432.10,
      "balanceDue": 5432.10,
      "dueDate": "2024-02-20T00:00:00Z",
      "paidInFullDate": null,
      "isPaid": false,
      "invoiceType": "Invoice",
      "daysPastDue": 5,
      "agingBucket": "30-60 Days"
    }
  ]
}
```

**Data Points Required:**
- ✅ Invoice: Number, Date, Amount, Balance Due
- ✅ Related Order: Order Number
- ✅ Customer: ID, Number, Name
- ✅ Payment: Due Date, Paid Date, Payment Status
- ✅ Aging: Days Past Due, Aging Bucket

---

### Endpoint: `GET /api/v1/customers/{customerId}/ar-balance`

**Purpose:** Get current AR balance for a customer

**CompUFloor Equivalent:**
```sql
SELECT
    ISNULL(SUM(ARO.ARO_INVOICE_BALANCE_DUE), 0) as ARBalance
FROM AR_OPEN_ITEM ARO
WHERE ARO.ARO_CUMMAS_ID = @CustomerId
    AND ARO.ARO_INVOICE_TYPE = 'I'
    AND ARO.ARO_DATE_PAID_IN_FULL IS NULL
```

**Required Response:**
```json
{
  "customerId": 12345,
  "currentARBalance": 12500.50,
  "creditLimit": 50000.00,
  "availableCredit": 37499.50,
  "creditUtilization": 25.00,
  "onCreditHold": false
}
```

---

### Endpoint: `GET /api/v1/ar/aging-summary`

**Purpose:** Get AR aging report for dashboard

**CompUFloor Equivalent:**
```sql
SELECT
    COUNT(DISTINCT CASE WHEN DATEDIFF(day, ARO.ARO_DUE_DATE, GETDATE()) <= 0 THEN ARO.ARO_INVOICE_NUMBER END) as DueUnder30,
    SUM(CASE WHEN DATEDIFF(day, ARO.ARO_DUE_DATE, GETDATE()) <= 0 THEN ARO.ARO_INVOICE_BALANCE_DUE ELSE 0 END) as DueUnder30Amount,
    COUNT(DISTINCT CASE WHEN DATEDIFF(day, ARO.ARO_DUE_DATE, GETDATE()) BETWEEN 1 AND 30 THEN ARO.ARO_INVOICE_NUMBER END) as Due30to60,
    -- ... similar for other aging buckets
FROM AR_OPEN_ITEM ARO
WHERE ARO.ARO_INVOICE_TYPE = 'I'
    AND ARO.ARO_DATE_PAID_IN_FULL IS NULL
    AND ARO.ARO_WHSMAS_ID IN (@WarehouseIds)
```

**Required Parameters:**
- `branchCode` (string, optional): Filter by branch
- `warehouseIds` (int[], optional): Filter by warehouse IDs

**Required Response:**
```json
{
  "pendingInvoices": {
    "count": 250,
    "amount": 125000.50
  },
  "agingBuckets": {
    "current": { "count": 100, "amount": 50000.00 },
    "days1to30": { "count": 75, "amount": 35000.00 },
    "days31to60": { "count": 40, "amount": 20000.00 },
    "days61to90": { "count": 20, "amount": 12000.00 },
    "days91to120": { "count": 10, "amount": 5000.50 },
    "over120": { "count": 5, "amount": 3000.00 }
  },
  "topDelinquentCustomers": [
    {
      "customerId": 12345,
      "customerNumber": "10001",
      "customerName": "Sunset Village Apartments",
      "balanceDue": 12500.50,
      "daysPastDue": 65
    }
  ]
}
```

---

### Endpoint: `GET /api/v1/invoices/overdue`

**Purpose:** Get overdue invoices

**CompUFloor Equivalent:**
```sql
SELECT
    ARO.ARO_INVOICE_NUMBER as InvoiceNumber,
    C.CUM_CUMMAS_ID as CustomerId,
    C.CUM_CUSTOMER_NAME as CustomerName,
    PC.IPC_DESCRIPTION as ManagementCompany,
    ARO.ARO_INVOICE_BALANCE_DUE as OutstandingAmount,
    ARO.ARO_DUE_DATE as DueDate,
    DATEDIFF(day, ARO.ARO_DUE_DATE, GETDATE()) as DaysPastDue,
    SM.SMN_SMNMAS_ID as SalesmanId,
    SM.SMN_SALESMAN_NAME as SalesmanName
FROM AR_OPEN_ITEM ARO
INNER JOIN CUSTOMER_MASTER C ON ARO.ARO_CUMMAS_ID = C.CUM_CUMMAS_ID
LEFT JOIN PRICE_CODES PC ON C.CUM_PRICE_CODE = PC.IPC_PRICE_CODE
LEFT JOIN SALESMAN_MASTER SM ON C.CUM_SMNMAS_ID = SM.SMN_SMNMAS_ID
WHERE ARO.ARO_INVOICE_TYPE = 'I'
    AND ARO.ARO_DATE_PAID_IN_FULL IS NULL
    AND ARO.ARO_DUE_DATE < GETDATE()
```

**Required Response:**
```json
{
  "items": [
    {
      "invoiceNumber": "INV-2023-1234",
      "customerId": 12345,
      "customerName": "Sunset Village Apartments",
      "managementCompany": "ABC Property Management",
      "outstandingAmount": 5432.10,
      "dueDate": "2023-12-15T00:00:00Z",
      "daysPastDue": 42,
      "agingBucket": "30-60 Days",
      "salesman": {
        "salesmanId": 42,
        "salesmanName": "Jane Doe"
      }
    }
  ],
  "summary": {
    "totalOverdueInvoices": 85,
    "totalOverdueAmount": 45000.00
  }
}
```

---

## 4️⃣ **SALES METRICS & ANALYTICS**

### Endpoint: `GET /api/v1/analytics/sales-metrics`

**Purpose:** Dashboard sales metrics (CRITICAL for app)

**CompUFloor Equivalent:**
```sql
SELECT
    COUNT(DISTINCT ARO.ARO_INVOICE_NUMBER) AS TotalOrders,
    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END) AS OnlineOrders,
    SUM(ARO.ARO_INVOICE_AMOUNT) AS TotalOrderAmount,
    AVG(ARO.ARO_INVOICE_AMOUNT) AS AverageOrderValue,
    COUNT(DISTINCT CASE WHEN ARO.ARO_DATE_PAID_IN_FULL IS NULL THEN ARO.ARO_INVOICE_NUMBER END) AS TotalInvoices
FROM AR_OPEN_ITEM ARO
INNER JOIN INVOICE_HEADER IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
LEFT JOIN SALES_HEADER S ON IHF.IHF_ORDER_NUMBER = S.SOH_NUMBER
WHERE ARO.ARO_INVOICE_TYPE = 'I'
  AND IHF.IHF_INVOICE_DATE BETWEEN @StartDate AND @EndDate
  AND ARO.ARO_WHSMAS_ID IN (@WarehouseIds)
  AND (@SalesmanId IS NULL OR S.SOH_SMNMAS_ID = @SalesmanId)
```

**Required Parameters:**
- `startDate` (datetime, required)
- `endDate` (datetime, required)
- `branchCode` (string, optional)
- `warehouseIds` (int[], optional)
- `salesmanId` (int, optional)

**Required Response:**
```json
{
  "period": {
    "startDate": "2024-01-01T00:00:00Z",
    "endDate": "2024-01-31T23:59:59Z"
  },
  "orders": {
    "totalOrders": 150,
    "onlineOrders": 75,
    "inStoreOrders": 75,
    "totalOrderAmount": 125000.50,
    "averageOrderValue": 833.34
  },
  "invoices": {
    "totalInvoices": 145,
    "totalRevenue": 120000.00
  },
  "locationCode": "LAX"
}
```

---

### Endpoint: `GET /api/v1/analytics/daily-orders`

**Purpose:** Daily order counts for charts

**CompUFloor Equivalent:**
```sql
SELECT
    DATENAME(weekday, S.SOH_ORDER_DATE) as WeekdayName,
    CAST(S.SOH_ORDER_DATE AS DATE) as OrderDate,
    COUNT(*) as OrdersCount,
    SUM(S.SOH_TOTAL_AMOUNT) as TotalOrderAmount
FROM SALES_HEADER S
WHERE S.SOH_ORDER_DATE BETWEEN @StartDate AND @EndDate
    AND S.SOH_WHSMAS_ID IN (@WarehouseIds)
GROUP BY CAST(S.SOH_ORDER_DATE AS DATE), DATENAME(weekday, S.SOH_ORDER_DATE)
ORDER BY CAST(S.SOH_ORDER_DATE AS DATE)
```

**Required Response:**
```json
{
  "items": [
    {
      "date": "2024-01-15T00:00:00Z",
      "weekdayName": "Monday",
      "ordersCount": 12,
      "totalOrderAmount": 8500.00
    },
    {
      "date": "2024-01-16T00:00:00Z",
      "weekdayName": "Tuesday",
      "ordersCount": 15,
      "totalOrderAmount": 10200.50
    }
  ]
}
```

---

### Endpoint: `GET /api/v1/analytics/salesman-performance`

**Purpose:** Salesman performance metrics

**CompUFloor Equivalent:**
```sql
SELECT
    SM.SMN_SMNMAS_ID as SalesmanId,
    SM.SMN_SALESMAN_NAME as SalesmanName,
    SUM(CASE WHEN S.SOH_ORDER_DATE >= @MTDStart THEN S.SOH_TOTAL_AMOUNT ELSE 0 END) as MTDSales,
    SUM(CASE WHEN S.SOH_ORDER_DATE >= @YTDStart THEN S.SOH_TOTAL_AMOUNT ELSE 0 END) as YTDSales,
    COUNT(CASE WHEN S.SOH_ORDER_DATE >= @MTDStart THEN 1 END) as MTDOrderCount,
    COUNT(CASE WHEN S.SOH_ORDER_DATE >= @YTDStart THEN 1 END) as YTDOrderCount
FROM SALESMAN_MASTER SM
LEFT JOIN SALES_HEADER S ON SM.SMN_SMNMAS_ID = S.SOH_SMNMAS_ID
WHERE S.SOH_WHSMAS_ID IN (@WarehouseIds)
GROUP BY SM.SMN_SMNMAS_ID, SM.SMN_SALESMAN_NAME
```

**Required Response:**
```json
{
  "items": [
    {
      "salesmanId": 42,
      "salesmanName": "Jane Doe",
      "mtdSales": 45000.00,
      "ytdSales": 125000.50,
      "mtdOrderCount": 25,
      "ytdOrderCount": 85
    }
  ]
}
```

---

## 5️⃣ **REFERENCE DATA**

### Endpoint: `GET /api/v1/salesmen`

**Purpose:** Get list of salesmen/sales reps

**CompUFloor Equivalent:**
```sql
SELECT
    SMN_SMNMAS_ID as SalesmanId,
    SMN_SALESMAN_NAME as SalesmanName,
    SMN_SALESMAN_NUMBER as SalesmanNumber
FROM SALESMAN_MASTER
ORDER BY SMN_SALESMAN_NAME
```

**Required Response:**
```json
{
  "items": [
    {
      "salesmanId": 42,
      "salesmanName": "Jane Doe",
      "salesmanNumber": "SD-001",
      "isActive": true
    }
  ]
}
```

---

### Endpoint: `GET /api/v1/price-codes`

**Purpose:** Get pricing tiers/codes

**CompUFloor Equivalent:**
```sql
SELECT
    IPC_PRICE_CODE as PriceCode,
    IPC_DESCRIPTION as Description
FROM PRICE_CODES
ORDER BY IPC_PRICE_CODE
```

**Required Response:**
```json
{
  "items": [
    {
      "priceCode": 5,
      "description": "Standard Pricing"
    },
    {
      "priceCode": 10,
      "description": "Volume Discount"
    }
  ]
}
```

---

### Endpoint: `GET /api/v1/warehouses`

**Purpose:** Get warehouses/branches

**CompUFloor Equivalent:**
```sql
SELECT
    WHS_WHSMAS_ID as WarehouseId,
    WHS_WAREHOUSE_NUMBER as WarehouseNumber,
    WHS_WAREHOUSE_NAME as WarehouseName
FROM WAREHOUSE_MASTER
ORDER BY WHS_WAREHOUSE_NUMBER
```

**Required Response:**
```json
{
  "items": [
    {
      "warehouseId": 1,
      "warehouseNumber": "001",
      "warehouseName": "Los Angeles Warehouse",
      "locationCode": "LAX"
    },
    {
      "warehouseId": 2,
      "warehouseNumber": "002",
      "warehouseName": "Las Vegas Warehouse",
      "locationCode": "LSV"
    }
  ]
}
```

---

## 6️⃣ **AUTHENTICATION & AUTHORIZATION**

### Required Authentication Methods:

**Option 1: API Key (Preferred for simplicity)**
```
GET /api/v1/customers
Authorization: Bearer sk_live_abc123def456...
X-Tenant-Id: seamless-lax
```

**Option 2: OAuth 2.0 (More secure)**
```
POST /oauth/token
{
  "grant_type": "client_credentials",
  "client_id": "seamless-salesmetrics",
  "client_secret": "...",
  "scope": "read:customers read:orders read:invoices"
}

Response:
{
  "access_token": "eyJhbGc...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

### Required Scopes:
- `read:customers` - Read customer/property data
- `read:orders` - Read sales orders
- `read:invoices` - Read invoices and AR data
- `read:analytics` - Read sales metrics
- `read:reference` - Read salesmen, warehouses, price codes

---

## 7️⃣ **MULTI-TENANT SUPPORT**

Since Seamless Flooring has 5 locations (LAX, LSV, CHN, PHX, SND), Kudu needs to support:

### Option A: Separate Tenant IDs per Location
```json
{
  "LAX": { "tenantId": "seamless-lax" },
  "LSV": { "tenantId": "seamless-lsv" },
  "CHN": { "tenantId": "seamless-chn" },
  "PHX": { "tenantId": "seamless-phx" },
  "SND": { "tenantId": "seamless-snd" }
}
```

### Option B: Single Tenant with Branch Filter
```
GET /api/v1/customers?branchCode=LAX
X-Tenant-Id: seamless-flooring
```

**Preferred:** Option A (separate tenants) for better data isolation

---

## 8️⃣ **DATA SYNC & HISTORICAL DATA**

### Important Requirements:

**CompUFloor Historical Data:**
- ✅ Keep CompUFloor databases for historical data (pre-migration)
- ✅ SalesMetrics will query CompUFloor for dates before migration
- ✅ SalesMetrics will query Kudu for dates after migration

**Example:**
```csharp
// Get orders from 2020-2024 (before Kudu migration)
var historicalOrders = await compuFloorClient.GetOrdersAsync(
    startDate: new DateTime(2020, 1, 1),
    endDate: new DateTime(2024, 12, 31));

// Get orders from 2025+ (after Kudu migration)
var currentOrders = await kuduClient.GetOrdersAsync(
    startDate: new DateTime(2025, 1, 1),
    endDate: DateTime.Now);
```

**Migration Cutoff Date:**
- Need to track when each location migrated to Kudu
- Store in `ErpSettings` configuration
- Query appropriate provider based on date range

---

## 9️⃣ **PERFORMANCE REQUIREMENTS**

### Response Time:
- Simple queries (customer search): < 500ms
- Complex queries (sales metrics): < 2 seconds
- Bulk data (all customers): < 5 seconds

### Pagination:
- All list endpoints must support pagination
- Default page size: 20
- Max page size: 100

### Rate Limiting:
- Minimum: 100 requests/minute per tenant
- Preferred: 300 requests/minute per tenant

### Caching:
- SalesMetrics will implement client-side caching (5-10 minutes)
- Reference data can be cached longer (1 hour)

---

## 🔟 **ERROR HANDLING**

### Standard Error Response:
```json
{
  "error": {
    "code": "CUSTOMER_NOT_FOUND",
    "message": "Customer with ID 12345 was not found",
    "statusCode": 404,
    "timestamp": "2024-01-15T10:30:00Z",
    "requestId": "req_abc123"
  }
}
```

### Expected Error Codes:
- `400 Bad Request` - Invalid parameters
- `401 Unauthorized` - Invalid/missing API key
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error
- `503 Service Unavailable` - Kudu temporarily down

---

## 📊 **PRIORITY ENDPOINTS**

For initial Kudu integration, these are **CRITICAL** (must have):

**🔴 Critical (Phase 1):**
1. ✅ `GET /api/v1/customers/search` - Property search
2. ✅ `GET /api/v1/customers/{id}` - Property details
3. ✅ `GET /api/v1/customers/{id}/orders` - Orders for property
4. ✅ `GET /api/v1/analytics/sales-metrics` - Dashboard metrics
5. ✅ `GET /api/v1/ar/aging-summary` - AR aging report

**🟡 Important (Phase 2):**
6. ✅ `GET /api/v1/orders/{id}` - Order details
7. ✅ `GET /api/v1/customers/{id}/invoices` - Invoices
8. ✅ `GET /api/v1/invoices/overdue` - Overdue invoices
9. ✅ `GET /api/v1/salesmen` - Salesmen list

**🟢 Nice to Have (Phase 3):**
10. ✅ `GET /api/v1/analytics/daily-orders` - Daily metrics
11. ✅ `GET /api/v1/analytics/salesman-performance` - Salesman metrics
12. ✅ `GET /api/v1/warehouses` - Warehouse list
13. ✅ `GET /api/v1/price-codes` - Price codes

---

## 📝 **NEXT STEPS**

### For Seamless Flooring Team:
1. ✅ Share this document with Kudu Pro support team
2. ✅ Request API documentation if available
3. ✅ Request sandbox/test environment access
4. ✅ Schedule API integration kickoff call with Kudu
5. ✅ Obtain API credentials (keys, tenant IDs)

### For Kudu Pro Team:
1. ❓ Review data requirements and confirm feasibility
2. ❓ Provide API documentation (OpenAPI/Swagger preferred)
3. ❓ Provide test/sandbox environment credentials
4. ❓ Confirm authentication method (API Key vs OAuth)
5. ❓ Provide tenant IDs for 5 locations (LAX, LSV, CHN, PHX, SND)
6. ❓ Confirm rate limits and performance characteristics
7. ❓ Schedule integration support calls

---

## 📞 **CONTACT INFORMATION**

**SalesMetrics Integration:**
- Application: SalesMetrics by Seamless Flooring, LLC
- Locations: 5 branches (LAX, LSV, CHN, PHX, SND)
- Current ERP: CompUFloor (SQL Server)
- Target ERP: Kudu Pro Flooring Software

**Questions about this spec?**
- Review the implemented abstraction layer in `/Services/Erp/`
- See `MIGRATION_GUIDE.md` for architecture details

---

**Document Version:** 1.0
**Last Updated:** 2026-01-05
**Status:** Ready for Kudu review
