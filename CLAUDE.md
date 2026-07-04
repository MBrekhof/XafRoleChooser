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

Security filtering works by overriding `PermissionPolicyUser.Roles` (virtual) in `RoleChooserUserBase`. The override filters roles via `RoleFilterAccessor` (`ConcurrentDictionary<Guid, IActiveRoleFilter>` keyed by user ID). With `PermissionsReloadMode.NoCache`, permissions are re-evaluated per DbContext using only active roles.

Key flow: Login → `LoggedOn` initializes filter (all roles active) → main window shows → chooser popup auto-appears (skipped if <2 optional roles) → Accept: selected rows = active roles → `SetActiveRoles()` + `ReloadPermissions()` → recreate navigation → startup view. No mid-session switching; `Roles` override is pass-through unless the session was narrowed.

Note: XAF Blazor renders booleans as display-only SVGs in popup ListViews, so inline editing doesn't work. Row selection is used instead. `ActiveRoleFilter` has an optional logger (debug logging removed from `IsRoleActive` to avoid spam). `AlwaysActiveRoleName` is cached on `IActiveRoleFilter` during `Initialize()`.

Consuming apps must: (1) inherit user from `RoleChooserUserBase`, (2) call `services.AddRoleChooser()`, (3) register `.Add<RoleChooserModule>()`, (4) assign **every user** the always-active role ("Default") — without it `AlwaysActiveRoleId` is null and the login-time selection can strip the user of all access (only logout/login recovers); the module does not validate this.

## Demo Business Entities

The demo app includes sample entities with role-based permissions to demonstrate how the active-role selection affects data access:

- **Company** (CRM group) — accessible to all roles
- **Employee** (HR group) — HR Manager role
- **Project** (Projects group) — Project Manager, Sales roles
- **Order** (Sales group) — Sales, Finance roles
- **OrderLine** (Sales group, separate file) — Sales, Finance roles
- **Invoice** (Finance group) — Finance, Sales roles

## Test Users (empty passwords)

- **Admin**: Default, Administrators, HR Manager, Project Manager, Sales, Finance (chooser appears)
- **MultiRole**: Default, Administrators, HR Manager, Project Manager, Sales, Finance (chooser appears)
- **User**: Default only (chooser skipped)
- **SingleRole**: Default, Sales (chooser skipped, both active)

## Database

- Default: LocalDB (`appsettings.json`)
- Docker: SQL Server 2022 (`appsettings.Development.json` has `DockerConnectionString`)
- EF Core migrations: `dotnet ef migrations add <Name> --project XafRoleChooser/XafRoleChooser.Module --startup-project XafRoleChooser/XafRoleChooser.Blazor.Server`
