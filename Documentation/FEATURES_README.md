# Sales Metrics App - Feature Guide

This guide explains **how each major feature works** in the current web app and how teams should describe it in operations/training.

## 1) Leadership Weekly Recap (currently GM Recap)

> **Rename recommendation:** “GM Recap” should transition to **Leadership Weekly Recap** because usage now spans Admin/Owners, Regionals, and GMs.

### What it does
- Captures weekly leadership observations, metrics, and notes through structured recap fields.
- Supports draft/final submission behavior per user, location, and week.
- Stores multi-entry field values for recap activity history.

### How it works
- Entry page loads current week and user/location context from claims/session.
- Existing recap content is loaded for the same user + location + week.
- On submit, the system either updates an existing weekly entry or creates a new one, then inserts field values.
- Cached recap list data is invalidated after changes to keep list views fresh.

### Key app surfaces
- `/GMRecap/RecapEntry`
- `/GMRecap/RecapList`
- Controller: `Controllers/GMRecapController.cs`

---

## 2) Dashboards (GM, Office, Sales)

### What they do
- Provide at-a-glance KPI views by role and function.
- Separate dashboard experiences are shown based on feature access permissions.

### How they work
- Sidebar checks feature permissions (`GMDashboard`, `Dashboard`, `OfficeDashboard`).
- Available dashboard entries are rendered only when the user has explicit access.

### Key app surfaces
- `/GMDash/Index`
- `/Office/Index`
- `/Dashboard/Index`
- Sidebar/menu gating: `Views/Shared/sidebar.cshtml`

---

## 3) Envelope / Document Signing

### What it does
- Manages document envelopes, recipient routing, template-based sending, and status actions (resend/void).
- Supports internal template management and public signing flows.

### How it works
- Admin users access envelope lists and create/send workflows.
- Template metadata and template CRUD are managed in signing controllers.
- Notification and PDF-rendering services support email and rendered outputs.

### Key app surfaces
- `/SignAdmin/Index`, `/SignAdmin/Create`
- `/SignTemplates/Index`
- Controllers: `SignAdminController`, `SignTemplateController`, `SignPublicController`
- Models/domain: `Models/Signing/*`, `Domain/Signing/*`

---

## 4) Reports (Standard) + Report/Query Builder

### What they do
- **Reports:** curated reporting catalog for operational insights.
- **Query Builder:** user-driven report creation via multi-step wizard and allowed-table model.

### How Query Builder works
- Access is permission-gated (e.g., `REPORT_BUILDER_ACCESS`, `REPORT_BUILDER_CREATE`, `REPORT_BUILDER_EDIT`, admin/share/delete permissions).
- Create flow starts with allowed table selection, then column configuration, relationships, filters, and run/export.
- Supports multiple data source adapter registrations (SQL/API/Kudu).

### Key app surfaces
- `/Reports`
- `/ReportBuilder`, `/ReportBuilder/Create`
- Controller: `Controllers/ReportBuilderController.cs`
- Architecture docs: `docs/QueryBuilder_Architecture.md`, `docs/QueryBuilder_Architecture_MultiSource.md`

---

## 5) Notifications

### What it does
- Delivers in-app notifications with unread tracking, read/unread toggles, and per-user notification center views.
- Separates normal notifications from announcement/broadcast style notifications.

### How it works
- API-style endpoints fetch, mark read, mark all, toggle read state, and delete notification records.
- Notification center combines notification items with announcement/broadcast sources.
- Uses notification service + SignalR hub registration for realtime scenarios.

### Key app surfaces
- `/Notifications/Index`
- Controller: `Controllers/NotificationsController.cs`
- Hub: `Hubs/NotificationHub.cs`

---

## 6) Announcements

### What it does
- Enables organizational announcements with targeting, scheduling, expiration, templates, and priority.

### How it works
- Access restricted to authorized leadership roles (currently Admin + GM checks in controller).
- Create/edit/send/delete flows are service-backed.
- Targeting can use role and location context.

### Key app surfaces
- `/Announcements/Index`, `/Announcements/Create`
- Controller: `Controllers/AnnouncementsController.cs`
- Service namespace: `Services/Announcements/*`

---

## 7) Tasks and Calendar

### What it does
- Tracks individual and admin-level task execution and scheduling.
- Provides calendar view for planning follow-ups and activities.

### How it works
- “My Tasks,” “View All Tasks,” and “Calendar” are shown by permission and role.
- Google scopes for calendar/tasks are configured in authentication setup.

### Key app surfaces
- `/Tasks/Task`, `/Tasks/AdminTask`, `/Tasks/Schedule`
- Controller: `Controllers/TasksController.cs`

---

## 8) Management Company & Property Tracking

### What it does
- Maintains visibility into management companies and associated properties.
- Supports structured browsing/filtering from dedicated modules.

### How it works
- Sidebar exposes Management and Properties areas only with corresponding feature access.
- Dedicated controllers back each area’s views and interactions.

### Key app surfaces
- `/Management/Management`
- `/Properties/Properties`
- Controllers: `ManagementController`, `PropertiesController`

---

## 9) Job / Order Tracking

### What it does
- Provides work order/job schedule visibility for operational execution.

### How it works
- “Work Orders” section appears under Job Schedule when orders permission is granted.
- Orders data and APIs are handled through MVC and API controllers.

### Key app surfaces
- `/Orders/Orders`
- Controllers: `OrdersController`, `Controllers/Api/OrdersApiController.cs`

---

## 10) Sales Rep Reporting, Tracking, and Grading (Sales Pulse)

### What it does
- Tracks salesperson activity and outcomes, including account/order-oriented performance views.

### How it works
- Sales Pulse is permission-gated and role-sensitive.
- Controller logic filters and aggregates sales-related data across location/management contexts.

### Key app surfaces
- `/Salesperson/Index`
- Controller: `Controllers/SalespersonController.cs`

---

## 11) Form Creation / Onboarding / Data Capture

### What it does
- Supports structured form-driven workflows and onboarding experiences.
- Includes document templates and configurable template systems in documentation/planning assets.

### How it works
- Onboarding routes and controllers handle guided form-driven flows.
- Template and signing subsystems provide reusable structures for document/form use cases.

### Key app surfaces
- `Controllers/OnboardingController.cs`
- Signing template surfaces in `SignTemplateController`
- Supporting docs in `Documentation/TEMPLATE_*` files

---

## 12) Journal

### What it does
- Gives users a personal/internal journal for operational notes and categorized entries.

### How it works
- CRUD-style flow backed by SQL queries for entries and categories.

### Key app surfaces
- `/Journal/Index`
- Controller: `Controllers/JournalController.cs`

---

## 13) Prospecting / Yardi Integration

### What it does
- Brings Yardi property/prospecting information into the app.
- Includes upload capabilities for permitted users.

### How it works
- Feature permissions gate Yardi browsing and upload experiences independently.
- ERP abstraction and provider client setup supports broader data-provider evolution.

### Key app surfaces
- `/Yardi/YardiProperties`, `/Yardi/Upload`
- Controller: `Controllers/YardiController.cs`

---

## 14) Access Controls, Settings, and Error Monitoring

### What they do
- **Access Controls:** user/role feature permissions.
- **System Settings:** centralized app-level settings.
- **Error Monitoring:** error dashboard/log workflows for support and reliability.

### How they work
- Sidebar exposes modules based on `Users`, `Settings`, and `ErrorLogs` permissions.
- Global error middleware logs exceptions; dedicated error views expose diagnostics.

### Key app surfaces
- `/Accounts/Index`
- `/Settings/Index`
- `/ErrorLog/Dashboard`, `/ErrorLog/Index`
- Middleware: `Middleware/GlobalErrorHandlingMiddleware.cs`

---

## Feature visibility model (important)

Most features are surfaced from `Views/Shared/sidebar.cshtml` with explicit per-feature permission checks.

This means onboarding/admin teams can enable modules incrementally for each role or user without code changes.

---

## Recommended terminology update plan

1. Adopt **Leadership Weekly Recap** in docs immediately.
2. In UI, transition labels to `Leadership Weekly Recap (GM Recap)` for one release cycle.
3. Update route/display text in sidebar/help materials.
4. Keep legacy naming in technical references only where needed to avoid migration confusion.

