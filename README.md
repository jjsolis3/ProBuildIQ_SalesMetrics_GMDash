# ProBuildIQ Sales Metrics App

A role-aware ASP.NET Core MVC platform for sales operations, leadership reporting, property/order tracking, reporting, and internal communications.

## What this app is

The Sales Metrics App centralizes daily/weekly operational workflows into one web application:

- leadership dashboards and recap workflows
- task planning and calendar management
- management company and property tracking
- job/order visibility
- sales rep performance reporting
- custom reporting (standard reports + Query Builder)
- document envelopes and e-sign flows
- notifications and announcements
- admin controls, permissions, and settings

## Tech stack (current implementation)

- **Framework:** ASP.NET Core MVC
- **Data access:** SQL Server + EF Core + selected direct SQL queries
- **Auth:** Cookie auth + Google OAuth
- **Realtime:** SignalR (`/notificationHub`)
- **Reporting exports:** server-side report export services + Rotativa PDF tooling
- **Caching and performance:** memory cache + response compression + middleware instrumentation

## Application architecture at a glance

- **Entry point / startup configuration:** `Program.cs`
- **MVC features:** organized primarily by controllers and views per feature area
- **Business services:** `Services/*` namespaces for notifications, announcements, reporting, signing, permissions, and ERP integrations
- **Data model:** EF entities and domain models under `Models/*`, `Domain/*`, and `Data/*`
- **Feature permissions:** sidebar/menu visibility and endpoint access are permission-driven

## Role and permission model

The app combines:

1. **Role-based behavior** (Admin, Owner/President, Regional, GM, etc.)
2. **Feature-level permissions** (e.g., `REPORT_BUILDER_ACCESS`, `Envelopes`, `Announcements`)

This allows one feature to be visible/hidden per user while still supporting operational hierarchy.

## Feature documentation

For a complete feature-by-feature functional breakdown (including GM Recap renaming guidance), use:

- [`Documentation/FEATURES_README.md`](Documentation/FEATURES_README.md)
- [`Documentation/ERP_CONNECTIVITY_REVIEW_AND_API_MIGRATION_GUIDE.md`](Documentation/ERP_CONNECTIVITY_REVIEW_AND_API_MIGRATION_GUIDE.md)

## Naming update recommendation: GM Recap

Because the recap workflow now includes **Admin/Owner, Regional, and GM** usage, the feature should be documented and gradually relabeled as:

- **Leadership Weekly Recap** (preferred)
- with transitional alias: **“GM Recap (Leadership Weekly Recap)”**

This keeps continuity while reducing role-specific confusion in the UI and training materials.

## Suggested next documentation steps

- Add screenshots for each major feature page in a future docs pass.
- Add API endpoint catalog for AJAX/JSON endpoints used by reporting and notifications.
- Add environment-specific runbooks (local/dev/prod) for onboarding and troubleshooting.
