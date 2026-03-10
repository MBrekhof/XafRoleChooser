# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Start

On new session, read these files first: `SESSION_HANDOFF.md`, `TODO.md`

## Project Overview

Reusable DevExpress XAF module that lets users choose which roles are active after login. Built on .NET 8, DevExpress v25.2.3, EF Core with SQL Server. Includes a demo app (Blazor Server + WinForms) and Playwright tests.

## Solution Structure

- **`src/RoleChooser/`** — Reusable module (the main deliverable). Platform-agnostic, depends only on DevExpress.ExpressApp + Security.
- **`XafRoleChooser/XafRoleChooser.Module/`** — Demo app shared module (business objects, DbContext, seed data). References RoleChooser.
- **`XafRoleChooser/XafRoleChooser.Blazor.Server/`** — Demo Blazor Server frontend with Web API, JWT + Cookie auth.
- **`XafRoleChooser/XafRoleChooser.Win/`** — Demo WinForms frontend.
- **`tests/XafRoleChooser.Playwright/`** — Playwright E2E tests.
- **`docs/`** — Design docs and how-to-implement guide.

Solution file: `XafRoleChooser.slnx` (XML format).

## Build & Run

```bash
dotnet build XafRoleChooser.slnx
docker compose up -d                    # SQL Server 2022
dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server/XafRoleChooser.Blazor.Server.csproj
dotnet test tests/XafRoleChooser.Playwright/
```

## RoleChooser Module Architecture

Security filtering works by overriding `PermissionPolicyUser.Roles` (virtual) in `RoleChooserUserBase`. The override filters roles via `RoleFilterAccessor` (`AsyncLocal<IActiveRoleFilter>`). With `PermissionsReloadMode.NoCache`, permissions are re-evaluated per DbContext using only active roles.

Key flow: Login → `RoleChooserModule.LoggedOn` initializes filter → User clicks "Active Roles" (Tools tab) → PopupWindowShowAction shows role list with row-selection checkboxes → On Accept, selected rows = active roles via `PopupWindowViewSelectedObjects` → `SetActiveRoles()` + `ReloadPermissions()` → Close all tabs → Recreate navigation → Navigate to startup item.

Note: XAF Blazor renders booleans as display-only SVGs in popup ListViews, so inline editing doesn't work. Row selection is used instead. `RoleFilterAccessor` uses `ConcurrentDictionary<Guid, IActiveRoleFilter>` (AsyncLocal fails in Blazor Server).

Consuming apps must: (1) inherit user from `RoleChooserUserBase`, (2) call `services.AddRoleChooser()`, (3) register `.Add<RoleChooserModule>()`.

## Demo Business Entities

The demo app includes sample entities with role-based permissions to demonstrate role switching:

- **Company** (Company group) — accessible to all roles
- **Employee** (HR group) — HR Manager role
- **Project** (Projects group) — Project Manager, Sales roles
- **Order/OrderLine** (Sales group) — Sales, Finance roles
- **Invoice** (Finance group) — Finance, Sales roles

## Test Users (empty passwords)

- **Admin**: Administrators, HR Manager, Project Manager, Sales, Finance
- **User**: Default only
- **MultiRole**: Default, Administrators, HR Manager, Project Manager, Sales, Finance

## Database

- Default: LocalDB (`appsettings.json`)
- Docker: SQL Server 2022 (`appsettings.Development.json` has `DockerConnectionString`)
- EF Core migrations: `dotnet ef migrations add <Name> --project XafRoleChooser/XafRoleChooser.Module --startup-project XafRoleChooser/XafRoleChooser.Blazor.Server`
