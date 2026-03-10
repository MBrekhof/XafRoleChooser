# Session Handoff

**Read this file first when starting a new session.**

Also read: `TODO.md`, `CLAUDE.md`, `docs/how-to-implement.md`

## Project State

**Reusable XAF module** that lets users choose which roles are active after login via a toolbar popup with row-selection checkboxes. No restart needed — permissions update live.

## Current Status

**Phase: Implementation complete. Verified end-to-end. All 17/17 E2E tests pass.**

All code is written and the full solution builds clean. Docker, Playwright tests, and documentation are in place.

### Key changes since initial implementation

- **Row selection instead of inline editing** — XAF Blazor renders boolean columns in popup ListViews as display-only SVGs. The popup now uses row-selection checkboxes (`PopupWindowViewSelectedObjects`) instead of an `IsActive` toggle column.
- **Tab closing on role switch** — After accepting the role chooser, all open tabs are closed via reflection (`ChildTemplates` + `CloseViewTemplate()`) to prevent unauthorized access to views the user no longer has permissions for.
- **Navigation rebuild** — `ShowNavigationItemController.RecreateNavigationItems()` rebuilds the nav tree after role switch, then navigates to the startup item.
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

## Architecture Quick Reference

| Component | Location | Purpose |
|---|---|---|
| `IActiveRoleFilter` | `src/RoleChooser/Services/` | Interface: get/set active role IDs per session |
| `ActiveRoleFilter` | `src/RoleChooser/Services/` | Scoped implementation |
| `ActiveRoleSelection` | `src/RoleChooser/BusinessObjects/` | NonPersistent BO for popup ListView (RoleName visible, IsActive/RoleId hidden) |
| `RoleChooserWindowController` | `src/RoleChooser/Controllers/` | PopupWindowShowAction in toolbar, tab closing, nav rebuild |
| `RoleChooserUserBase` | `src/RoleChooser/Security/` | Base class overriding Roles property |
| `RoleFilterAccessor` | `src/RoleChooser/Security/` | ConcurrentDictionary-based ambient accessor |
| `RoleChooserModule` | `src/RoleChooser/` | Module definition, LoggedOn hook |
| `AddRoleChooser()` | `src/RoleChooser/` | DI registration extension |

## Demo Business Objects

| Entity | Nav Group | Roles with Access |
|---|---|---|
| Company | Company | All roles (shared) |
| Employee | HR | HR Manager |
| Project | Projects | Project Manager, Sales |
| Order/OrderLine | Sales | Sales, Finance |
| Invoice | Finance | Finance, Sales |

## Test Users (all empty passwords)

| User | Roles |
|---|---|
| Admin | Administrators, HR Manager, Project Manager, Sales, Finance |
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

1. ~~Run the app against Docker SQL Server, verify toolbar button appears and role switching works~~ — Done
2. ~~Run Playwright tests, fix selectors/timing for actual XAF Blazor markup~~ — Done (17/17 pass)
3. Test WinForms frontend — not yet verified
