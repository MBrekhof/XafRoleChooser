# Session Handoff

**Read this file first when starting a new session.**

Also read: `TODO.md`, `CLAUDE.md`, `docs/how-to-implement.md`

## Project State

**Reusable XAF module** that lets users choose which roles are active after login via a toolbar multi-select dropdown. "Default" role is always active. No restart needed.

## Current Status

**Phase: Implementation complete. Verified end-to-end. All 16/16 E2E tests pass.**

All code is written and the full solution builds clean (0 warnings, 0 errors). Docker, Playwright tests, and documentation are in place. The role loading bug (where `AllRoles` returned filtered results because it used the overridden `Roles` navigation property) has been fixed by switching to a `GetAllRoles()` method that loads roles via raw SQL from the `PermissionPolicyRolePermissionPolicyUser` join table. The `NonPersistentObjectSpace.ObjectsGetting` event is used to populate the popup ListView.

## Key Decisions Made

1. **Reusable module** — standalone package at `src/RoleChooser/`
2. **Multi-select popup with Apply** — PopupWindowShowAction with ListView of ActiveRoleSelection
3. **Security mechanism** — `RoleChooserUserBase` overrides `PermissionPolicyUser.Roles` (virtual), filtering via `AsyncLocal<IActiveRoleFilter>` (RoleFilterAccessor)
4. **No restart** — `PermissionsReloadMode.NoCache` + `ReloadPermissions()` on Apply
5. **Service registration** — `AddRoleChooser()` extension method (XAF ModuleBase has no ConfigureServices)
6. **No platform-specific projects** — WindowController works cross-platform

## Architecture Quick Reference

| Component | Location | Purpose |
|---|---|---|
| `IActiveRoleFilter` | `src/RoleChooser/Services/` | Interface: get/set active role IDs per session |
| `ActiveRoleFilter` | `src/RoleChooser/Services/` | Scoped implementation |
| `ActiveRoleSelection` | `src/RoleChooser/BusinessObjects/` | NonPersistent BO for checklist popup |
| `RoleChooserWindowController` | `src/RoleChooser/Controllers/` | PopupWindowShowAction in toolbar |
| `RoleChooserUserBase` | `src/RoleChooser/Security/` | Base class overriding Roles property |
| `RoleFilterAccessor` | `src/RoleChooser/Security/` | AsyncLocal ambient accessor |
| `RoleChooserModule` | `src/RoleChooser/` | Module definition, LoggedOn hook |
| `AddRoleChooser()` | `src/RoleChooser/` | DI registration extension |

## Test Users (all empty passwords)

| User | Roles |
|---|---|
| Admin | Default, Administrators, Manager, Reports |
| User | Default |
| MultiRole | Default, Administrators, Manager, DataEntry, Reports |

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
2. ~~Run Playwright tests, fix selectors/timing for actual XAF Blazor markup~~ — Done (16/16 pass)
3. Test WinForms frontend — not yet verified
