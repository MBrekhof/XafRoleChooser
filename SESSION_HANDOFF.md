# Session Handoff

**Read this file first when starting a new session.**

Also read: `TODO.md`, `CLAUDE.md`, `docs/how-to-implement.md`

## Project State

**Reusable XAF module** that lets users choose which roles are active after login via a toolbar popup with row-selection checkboxes. No restart needed — permissions update live.

## Current Status

**Phase: Implementation complete. All bugs fixed. Verified on Blazor + WinForms. 17/17 E2E tests pass. Ready to push.**

### Key changes since initial implementation

- **Row selection instead of inline editing** — XAF Blazor renders boolean columns in popup ListViews as display-only SVGs. The popup now uses row-selection checkboxes (`PopupWindowViewSelectedObjects`) instead of an `IsActive` toggle column.
- **Tab closing on role switch (both platforms)** — After accepting the role chooser, all open tabs are closed to prevent unauthorized access. Blazor: `BlazorWindow.Close()` via `MainWindow.MdiChildWindows`. WinForms: `ShowViewStrategy.Inspectors`. Both use reflection to stay platform-agnostic.
- **Navigation rebuild** — `ShowNavigationItemController.RecreateNavigationItems()` rebuilds the nav tree after role switch, then navigates to the startup item.
- **Company nav group renamed to CRM** — Entity name "Company" conflicted with nav group name in XAF, causing the group to not appear with IsAdministrative roles. Renamed to "CRM" in `[NavigationItem("CRM")]` and all nav permission paths.
- **OrderLine split into separate file** — `OrderLine.cs` extracted from `Order.cs`, has `[DefaultClassOptions]` and `[NavigationItem("Sales")]`.
- **Finance role has OrderLine_ListView nav permission** — Finance can see OrderLines in the Sales nav group.
- **Admin user has Default role** — The always-active role is now assigned to Admin (was missing previously).
- **ApplicationUser has [DefaultClassOptions]** — Admin can see ApplicationUser in the navigation.
- **ActiveRoleFilter logger is optional** — Constructor uses `ILogger<ActiveRoleFilter>? logger = null`. Debug logging removed from `IsRoleActive` to avoid log spam during permission evaluation.
- **AlwaysActiveRoleName cached on IActiveRoleFilter** — Set during `Initialize()`, used in logging without repeated lookups.
- **Sample business entities** — Demo app includes Company, Employee, Project, Order/OrderLine, and Invoice entities with role-based permissions and realistic seed data.
- **Serilog structured logging** — Console + File sinks throughout the RoleChooser module and Blazor Server host.
- **Blazor Server session handling** — `RoleFilterAccessor` uses `ConcurrentDictionary<Guid, IActiveRoleFilter>` keyed by user ID (AsyncLocal fails across Blazor async boundaries).

## Key Decisions Made

1. **Reusable module** — standalone package at `src/RoleChooser/`
2. **Row-selection popup** — PopupWindowShowAction with ListView; selected rows = active roles
3. **Security mechanism** — `RoleChooserUserBase` overrides `PermissionPolicyUser.Roles` (virtual), filtering via `RoleFilterAccessor`
4. **No restart** — `PermissionsReloadMode.NoCache` + `ReloadPermissions()` on Accept
5. **Service registration** — `AddRoleChooser()` extension method (XAF ModuleBase has no ConfigureServices)
6. **No platform-specific projects** — WindowController works cross-platform
7. **Close all tabs on role switch** — Prevents security issue where open tabs remain accessible after losing permissions
8. **CRM nav group** — Company entity uses `[NavigationItem("CRM")]` to avoid XAF nav group name conflict

## Architecture Quick Reference

| Component | Location | Purpose |
|---|---|---|
| `IActiveRoleFilter` | `src/RoleChooser/Services/` | Interface: get/set active role IDs per session |
| `ActiveRoleFilter` | `src/RoleChooser/Services/` | Scoped implementation (optional logger) |
| `ActiveRoleSelection` | `src/RoleChooser/BusinessObjects/` | NonPersistent BO for popup ListView (RoleName visible, IsActive/RoleId hidden) |
| `RoleChooserWindowController` | `src/RoleChooser/Controllers/` | PopupWindowShowAction in toolbar, tab closing, nav rebuild |
| `RoleChooserUserBase` | `src/RoleChooser/Security/` | Base class overriding Roles property |
| `RoleFilterAccessor` | `src/RoleChooser/Security/` | ConcurrentDictionary-based ambient accessor |
| `RoleChooserModule` | `src/RoleChooser/` | Module definition, LoggedOn hook |
| `AddRoleChooser()` | `src/RoleChooser/` | DI registration extension |

## Demo Business Objects

| Entity | Nav Group | Roles with Access |
|---|---|---|
| Company | CRM | All roles (shared) |
| Employee | HR | HR Manager |
| Project | Projects | Project Manager, Sales |
| Order | Sales | Sales, Finance |
| OrderLine | Sales | Sales, Finance |
| Invoice | Finance | Finance, Sales |

## Test Users (all empty passwords)

| User | Roles |
|---|---|
| Admin | Default, Administrators, HR Manager, Project Manager, Sales, Finance |
| User | Default |
| MultiRole | Default, Administrators, HR Manager, Project Manager, Sales, Finance |

## How to Run

```bash
docker compose up -d                    # Start SQL Server
dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server/XafRoleChooser.Blazor.Server.csproj
# Install Playwright browsers (first time only):
pwsh tests/XafRoleChooser.Playwright/bin/Debug/net8.0/playwright.ps1 install
dotnet test tests/XafRoleChooser.Playwright/
```

## Completed Verification

1. ~~Run the app against Docker SQL Server, verify toolbar button appears and role switching works~~ -- Done
2. ~~Run Playwright tests, fix selectors/timing for actual XAF Blazor markup~~ -- Done (17/17 pass)
3. ~~Test WinForms frontend~~ -- Done, tab closing works via ShowViewStrategy.Inspectors
