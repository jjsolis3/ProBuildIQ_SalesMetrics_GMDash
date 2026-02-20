# ERP Connectivity Review and API Migration Guide

## Purpose

This document provides:

1. A **comprehensive review** of how SalesMetrics currently connects to databases/ERP.
2. A practical **implementation guide** for moving from direct SQL ERP access to an API-based ERP connection.

---

## 1) Current connectivity landscape

## 1.1 Two data domains in the app

The app currently uses two distinct data domains:

- **SalesMetrics application database** (`ConnectionStrings:SalesMetrics`):
  - authentication/session/profile/permissions/settings
  - notifications/announcements/tasks/signing metadata
  - report definitions and query builder metadata
- **ERP databases** (currently CompUFloor SQL databases by location):
  - LAX / LSV / CHN / PHX / SND connection strings
  - operational sales/order/invoice/customer data

## 1.2 Startup registrations (what is wired today)

In DI/startup:

- `ErpSettings` is bound from configuration.
- `CompUFloorErpClient` and `KuduErpClient` are registered.
- `ErpClientFactory` is registered and can select provider by location.
- `IErpDataClient` is currently registered as **CompUFloor by default** for backward compatibility.
- Query Builder data source adapters are registered for `SQL`, `API`, and `Kudu` source types.

**Important:** the factory exists, but not every code path uses it consistently yet.

## 1.3 Current provider model status

### CompUFloor (direct SQL)
- Implemented with many concrete methods in `CompUFloorErpClient`.
- Uses location-based SQL connection strings and Dapper/ADO.NET queries.
- Serves as the active production-grade ERP implementation.

### Kudu (API)
- API-capable client structure exists (`KuduErpClient`).
- Only early endpoints are implemented (`SearchPropertiesAsync`, `GetPropertyByIdAsync`).
- Most interface methods still throw `NotImplementedException` pending API contract completion.

### Generic HTTP client
- `HttpErpDataClient` exists as a placeholder and is largely unimplemented.

## 1.4 Controller/service coupling reality (critical for migration)

The codebase is **hybrid** right now:

- Some modules call ERP through `ErpClientFactory` + `IErpDataClient` (good for migration).
- Many modules still perform direct SQL calls (`SqlConnection`) in controllers/services.

This means flipping provider config alone will **not** fully migrate the app to API ERP. Direct SQL paths must be refactored to provider-agnostic service methods.

## 1.5 Query Builder status

- `SqlDataSourceAdapter` is mature and uses SQL metadata/execution patterns.
- API/Kudu adapters are registered but are not yet feature-complete equivalent.
- Query Builder currently depends heavily on SQL metadata concepts (`INFORMATION_SCHEMA`, FK metadata), which must be reinterpreted for API data sources.

---

## 2) What must be ready before API ERP cutover

To migrate safely, the following are required.

## 2.1 External API contract requirements (ERP vendor)

A complete, stable ERP API contract is needed for:

- properties/customers (search/list/detail)
- orders (list/detail/date-range)
- invoices/AR aging/open balances/overdue
- sales metrics aggregates
- reference data (sales reps, warehouses, price codes)
- pagination, filtering, sorting semantics
- rate limits, timeout/SLA, and error model

If Kudu is target ERP, align this with existing Kudu requirements documentation already in the repo.

## 2.2 Security and configuration requirements

Move ERP secrets/configuration to secure config management:

- environment variables or managed secret store (not plain appsettings for prod)
- per-location API credentials/tenant headers where applicable
- token refresh strategy if OAuth is required

## 2.3 Full `IErpDataClient` implementation for API provider

The target API client must implement all interface methods that current dashboards/reports/pages rely on, not just property lookup.

Until this is complete, fallback direct SQL will remain necessary.

---

## 3) Required internal code updates (the real migration checklist)

## 3.1 Standardize all ERP reads behind `IErpDataClient`

Refactor direct SQL ERP access in controllers/services into provider-agnostic service calls.

**High-priority areas:**
- dashboard/reporting controllers using direct location SQL
- service classes that directly open location ERP connections
- any helper/query logic pulling CompUFloor tables directly

Goal: UI/controller code should not know whether ERP data came from SQL or API.

## 3.2 Use `ErpClientFactory` consistently per request context

Any component needing ERP data should resolve provider from location context using `ErpClientFactory`.

This ensures location-by-location provider rollout (e.g., one office moves to API first).

## 3.3 Update report execution path

`ReportRunner` currently depends on injected default `IErpDataClient` (CompUFloor binding). For true multi-provider execution:

- resolve client via `ErpClientFactory` using `ReportUserContext` location
- avoid assuming raw SQL query execution (`QueryAsync`) is available for API providers
- where needed, convert SQL reports to semantic/domain report methods

## 3.4 Query Builder API parity plan

For API ERP support in Query Builder:

- define API metadata endpoint format for table/entity + field discovery
- define relationships model if SQL FK metadata is unavailable
- define supported filters/sorts/aggregates per source
- implement `ApiDataSourceAdapter`/`KuduDataSourceAdapter` to run query definitions against API endpoints
- document unsupported features and fallback behavior

## 3.5 Add resiliency and observability for API calls

Add production-grade behaviors:

- retry with exponential backoff for transient failures
- circuit-breaker/fallback strategy (if dual-provider period is used)
- structured logging including location/provider/request ID
- API latency/error dashboards

## 3.6 Test coverage and validation gates

Before cutover:

- contract tests for all `IErpDataClient` methods
- regression tests for dashboards, orders, properties, reports, sales pulse
- side-by-side comparison tests (SQL vs API results) for key KPIs
- staged rollout validation by location

---

## 4) Suggested phased migration plan

## Phase 0 - Discovery and inventory
- Catalog every direct ERP SQL usage and classify by feature criticality.
- Mark each as `Refactor Required` vs `Already Abstracted`.

## Phase 1 - API client completeness
- Implement missing methods in `KuduErpClient` (or new ERP API client).
- Add robust error handling and auth lifecycle.

## Phase 2 - Abstraction enforcement
- Refactor direct ERP SQL in controllers/services to use ERP service abstraction.
- Ensure all ERP paths derive provider by location context.

## Phase 3 - Parallel-run validation
- Run SQL and API side-by-side for selected locations/time windows.
- Compare sales totals, order counts, AR aging, and rep metrics.

## Phase 4 - Progressive cutover
- Switch one location to API provider in `ErpSettings`.
- Monitor telemetry and business KPI parity.
- Expand to remaining locations.

## Phase 5 - Post-cutover cleanup
- Remove dead SQL-only ERP code paths where no longer needed.
- Keep historical query strategy only if business requires pre-migration history from legacy DB.

---

## 5) Configuration model for cutover

Use `ErpSettings:Locations:{LocationCode}:Provider` to control location-level routing:

- `CompUFloor` = direct SQL ERP
- `Kudu` (or new provider name) = API ERP

For API providers, populate `ApiSettings`:

- `BaseUrl`
- `ApiKey` and/or OAuth settings
- `TenantId` if needed
- timeout/retry policy settings

This supports mixed-mode migration by office.

---

## 6) Practical “definition of done” for API readiness

A location is API-ready when all are true:

1. Feature-critical ERP methods are implemented in API client.
2. No critical UI flow for that location depends on direct ERP SQL.
3. KPI parity checks pass (within agreed tolerance) between SQL and API.
4. Observability dashboards show acceptable reliability/latency.
5. Rollback toggle exists (provider can be switched back per location).

---

## 7) Reference files to use during implementation

- Startup and DI wiring: `Program.cs`
- ERP abstraction contract: `Services/Erp/IErpDataClient.cs`
- Provider selection: `Services/Erp/ErpClientFactory.cs`
- SQL ERP implementation: `Services/Erp/Clients/CompUFloorErpClient.cs`
- API ERP implementation scaffold: `Services/Erp/Clients/KuduErpClient.cs`
- Query Builder SQL adapter: `Services/Reports/QueryBuilder/DataSources/SqlDataSourceAdapter.cs`
- Existing Kudu requirement spec: `Documentation/KUDU_API_REQUIREMENTS.md`

---

## 8) Recommended immediate next actions

1. Build an “ERP data access inventory” issue list from all direct `SqlConnection` ERP paths.
2. Prioritize Dashboard + Reports + Sales Pulse for abstraction-first refactor.
3. Complete all high-usage `KuduErpClient` methods and add integration tests.
4. Change `ReportRunner` to provider-by-location resolution.
5. Run one-location pilot cutover with side-by-side KPI validation.

