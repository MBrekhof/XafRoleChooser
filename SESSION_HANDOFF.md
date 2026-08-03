# Session Handoff

**Read this file first when starting a new session.**

Also read: `TODO.md`, `CLAUDE.md`, `docs/how-to-implement.md`

## Project State

**Reusable XAF module** that lets an administrator choose which of their assigned roles are
active, **once, right after login**. The chooser is a popup that appears automatically on the
first view shown after logon; the selection takes effect immediately (no restart) and is
remembered per user until logout.

**Phase: implementation complete on Blazor + WinForms; 17/17 Playwright E2E tests pass.**
Open work is RC-007 (WinForms multi-select parity) and RC-008 (XafNavigationHub integration
follow-ups) — see `TODO.md`.

## Current Design

Superseded designs are listed under "History" at the bottom — do not reintroduce them.

- **Login-time selection, not mid-session switching.** `RoleChooserWindowController` subscribes
  to `XafApplication.ViewShown`, shows the popup on the first view after login, then
  unsubscribes. (`Window.ViewChanged` does not work: in XAF Blazor's tabbed MDI, views land on
  MDI child windows, never on the main window.) Changing the active set requires a re-login.
- **Admin-only, and only when it is useful.** The popup appears only for members of
  `RoleChooserModule.AdministratorRoleName` (default `"Administrators"`) who have **two or more**
  optional roles (anything besides `AlwaysActiveRoleName`, default `"Default"`). Everyone else
  logs in with all roles active and is never prompted.
- **Per-circuit filter resolution.** `RoleChooserUserBase.Roles` resolves `IActiveRoleFilter`
  from the user entity's **own** `ObjectSpace.ServiceProvider` — the DI scope of the Blazor
  circuit that materialized it. Each circuit gets its own scoped filter, so concurrent logins of
  one account do not clobber each other.
- **The `Roles` override is pass-through except when narrowed.** It returns a filtered snapshot
  only when `filter.OwnerUserId == this.ID` **and** the session actually narrowed the selection.
  This is what keeps role Link/Unlink edits on the User DetailView persisting normally, and what
  stops a narrowed admin from seeing (or saving) *other* users' roles filtered.
- **Sticky selection, server-side per user.** `RoleSelectionStore` (static, keyed by user id)
  persists the chosen set; it is re-applied silently at `LoggedOn` so a browser refresh — which
  rebuilds the circuit — does not re-prompt, and cleared at `LoggingOff` so the chooser returns
  on the next login.
- **`PermissionsReloadMode.NoCache` + `SecuritySystem.ReloadPermissions()`** on Accept make the
  selection take effect immediately. NoCache is XAF's default; the module warns at startup if a
  caching mode is detected.
- **Row selection, not a checkbox column.** XAF Blazor renders booleans in popup ListViews as
  display-only SVGs, so the popup reads `PopupWindowViewSelectedObjects` rather than
  `ActiveRoleSelection.IsActive`. This is exactly what RC-007 has to change for WinForms, where
  the generic grid defaults to single-row select.
- **WinForms does not re-navigate after Accept.** It raises
  `IActiveRoleFilter.SessionRolesApplied` (via `NotifySessionRolesApplied()`) instead;
  `NavigationHubWinController` refreshes the hub in place. Re-navigating the startup item used to
  open a second "Main" DashboardView tab and crash DocumentManager layout restore on re-logon.

## Architecture Quick Reference

| Component | Location | Purpose |
|---|---|---|
| `IActiveRoleFilter` / `ActiveRoleFilter` | `src/RoleChooser/Services/` | Scoped (per circuit) active-role set; `OwnerUserId`, `IsFiltering`, `SessionRolesApplied`. Optional logger. |
| `ActiveRoleSelection` | `src/RoleChooser/BusinessObjects/` | NonPersistent BO backing the popup ListView |
| `RoleChooserWindowController` | `src/RoleChooser/Controllers/` | `ViewShown` hook, `PopupWindowShowAction`, tab closing, nav rebuild |
| `RoleChooserUserBase` | `src/RoleChooser/Security/` | Overrides `PermissionPolicyUser.Roles` (virtual under EF Core) |
| `RoleSelectionStore` | `src/RoleChooser/Security/` | Static per-user sticky selection; set on Accept, cleared on `LoggingOff` |
| `RoleChooserModule` | `src/RoleChooser/` | Module definition, `LoggedOn`/`LoggingOff` hooks, role-name config |
| `AddRoleChooser()` | `src/RoleChooser/RoleChooserServiceExtensions.cs` | DI registration (XAF `ModuleBase` has no `ConfigureServices`) |

## Integration Requirements

A consuming app must: (1) inherit its user from `RoleChooserUserBase`, (2) call
`services.AddRoleChooser()`, (3) register `.Add<RoleChooserModule>()`, and (4) assign **every
user** the always-active role. Without (4), `AlwaysActiveRoleId` is null and a login-time
selection can strip the user of all access until they log out and back in — the module does not
validate this.

## Demo Business Objects

| Entity | Nav Group | Roles with Access |
|---|---|---|
| Company | CRM | All roles (shared) |
| Employee | HR | HR Manager |
| Project | Projects | Project Manager, Sales |
| Order | Sales | Sales, Finance |
| OrderLine | Sales | Sales, Finance |
| Invoice | Finance | Finance, Sales |

`Company` uses `[NavigationItem("CRM")]` because the entity name collided with the nav group
name, which stopped the group appearing for `IsAdministrative` roles. `OrderLine` lives in its
own file with `[DefaultClassOptions]`.

## Test Users (all empty passwords)

| User | Roles | Chooser |
|---|---|---|
| Admin | Default, Administrators, HR Manager, Project Manager, Sales, Finance | Appears |
| MultiRole | Default, Administrators, HR Manager, Project Manager, Sales, Finance | Appears |
| User | Default | Skipped |
| SingleRole | Default, Sales | Skipped (only 1 optional role) |

## Accepted Limitations

Surfaced by an xhigh code review and deliberately left as-is; all follow from the sticky store
being **server-side and keyed by user id**, so clearing cookies does not reset them — only an
in-app logout or an app restart does. Detail in `CLAUDE.md` and `README.md`.

- Sticky is not reconciled against current role membership: a newly-granted role stays inactive
  until the user logs off and back on.
- Narrowing is a Blazor-session concept only — Web API / OData / JWT requests get all roles.
- Concurrent sessions of one account share one selection; a logout in any of them clears it for
  all (and the clear runs in the cancellable `LoggingOff`, so a cancelled logout still drops it).

## How to Run

```bash
docker compose up -d                    # SQL Server 2022
dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server/XafRoleChooser.Blazor.Server.csproj
# Install Playwright browsers (first time only):
pwsh tests/XafRoleChooser.Playwright/bin/Debug/net8.0/playwright.ps1 install
dotnet test tests/XafRoleChooser.Playwright/
```

## History — superseded, do not reintroduce

- **Mid-session role switching via a toolbar action** (removed by RC-002). The `Roles` override
  had to return a detached copy while filtering, so an admin's Link/Unlink writes on the User
  DetailView silently vanished, and switching mid-session needed fragile forced teardown of open
  views and navigation.
- **`RoleFilterAccessor`, a process-wide `ConcurrentDictionary<Guid, IActiveRoleFilter>` keyed by
  user id** (removed by RC-006). A single per-user entry cannot isolate concurrent sessions.
  `AsyncLocal<T>` was tried first and does not survive Blazor Server's async boundaries.
