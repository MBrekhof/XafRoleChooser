# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Start

On new session, read these files first: `SESSION_HANDOFF.md`, `TODO.md`, `docs/plans/2026-03-10-role-chooser-design.md`

## Project Overview

DevExpress XAF (eXpressApp Framework) application with EF Core, targeting .NET 8. Uses DevExpress v25.2.3. The app has Blazor Server and WinForms frontends sharing a common module, plus a reusable Role Chooser module (in progress). Database is SQL Server (LocalDB for dev).

## Solution Structure

- **XafRoleChooser.Module** — Shared module: business objects, DbContext (`XafRoleChooserEFCoreDbContext`), XAF module definition, database updater. Both frontends reference this.
- **XafRoleChooser.Blazor.Server** — Blazor Server frontend with Web API (OData + Swagger), JWT authentication, cookie auth.
- **XafRoleChooser.Win** — WinForms frontend (net8.0-windows).

Solution file: `XafRoleChooser.slnx` (XML-based solution format). Build configurations: Debug, Release, EasyTest.

## Build Commands

```bash
dotnet build XafRoleChooser.slnx
dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server/XafRoleChooser.Blazor.Server.csproj
```

## Key Architecture Details

- **ORM:** EF Core with `XafRoleChooserEFCoreDbContext` (SQL Server). Uses deferred deletion, optimistic locking, change tracking notifications.
- **Security:** XAF integrated security mode with `PermissionPolicyRole` and custom `ApplicationUser` (supports OAuth via `ISecurityUserWithLoginInfo`, lockout via `ISecurityUserLockout`).
- **Auth (Blazor):** Dual auth — Cookie (UI login at `/LoginPage`) + JWT Bearer (Web API). JWT config in `appsettings.json` under `Authentication:Jwt`.
- **Web API:** OData v4.01 at `/api/odata`. Swagger available in Development mode. Business objects exposed via `AddXafWebApi` in `Startup.cs`.
- **Database seeding:** `Updater.cs` creates "Admin" and "User" accounts with empty passwords and "Administrators"/"Default" roles in non-Release builds.
- **Connection string:** Configured in `appsettings.json` as `ConnectionStrings:ConnectionString`. Default points to LocalDB.

## EF Core Migrations

Migrations run from the Module project against `XafRoleChooserEFCoreDbContext`:

```bash
dotnet ef migrations add <Name> --project XafRoleChooser/XafRoleChooser.Module --startup-project XafRoleChooser/XafRoleChooser.Blazor.Server
dotnet ef database update --project XafRoleChooser/XafRoleChooser.Module --startup-project XafRoleChooser/XafRoleChooser.Blazor.Server
```

## DevExpress XAF Conventions

- Business objects go in `XafRoleChooser.Module/BusinessObjects/`.
- Model customizations use `.xafml` files (embedded resources in Module, content files in frontends).
- Module registration: add types in `XafRoleChooserModule` constructor (`AdditionalExportedTypes`) and register DbSets in the DbContext.
- Database seed logic lives in `DatabaseUpdate/Updater.cs`.
