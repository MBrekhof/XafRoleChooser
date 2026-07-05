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

Security filtering works by overriding `PermissionPolicyUser.Roles` (virtual) in `RoleChooserUserBase`. The override resolves the per-session `IActiveRoleFilter` from the user object's **own** `ObjectSpace.ServiceProvider` (the circuit-scoped DI container; `ObjectSpace` is the inherited `IObjectSpaceLink` member the EFCoreObjectSpace populates). No process-wide static — each Blazor circuit has its own scoped filter, so concurrent sessions don't clobber each other. The override narrows roles **only** when `filter.OwnerUserId == this.ID`, i.e. only the logged-in user's own object — never other users loaded in the session (Users ListView rows, another user's DetailView), otherwise their roles would display/save filtered. With `PermissionsReloadMode.NoCache`, permissions are re-evaluated per DbContext using only active roles.

The chooser is **admin-only**: it appears only for members of `RoleChooserModule.AdministratorRoleName` (default "Administrators"). The selection is **sticky per user** (`RoleSelectionStore`, process-wide, keyed by user id): applied silently on login if present so a browser refresh (new circuit) does not re-prompt; cleared on explicit logout (`LoggingOff`) so the chooser returns next login.

Key flow: Login → `LoggedOn` initializes the scoped filter (all roles active), then re-applies any sticky selection → main window shows → chooser popup auto-appears (only if admin, ≥2 optional roles, and no sticky selection) → Accept: selected rows = active roles → `SetActiveRoles()` + persist to `RoleSelectionStore` + `ReloadPermissions()` → recreate navigation → startup view. No mid-session switching; `Roles` override is pass-through unless the logged-in user's own session was narrowed.

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
