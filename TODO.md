# TODO — XAF Role Chooser

## P1: High — Login-Time Role Selection Redesign

The redesign (RC-001..RC-004) landed on `master`: roles are chosen once at login via a popup
interstitial (skipped when <2 optional roles), the `Roles` override is pass-through unless the
session was narrowed (fixes the WLNCentral ROLE-001 write-loss bug), and the mid-session switch
machinery is gone. The popup is triggered by `XafApplication.ViewShown` (not `Frame.ViewChanged`
— views never land on the Blazor MDI main window). Plan:
`docs/superpowers/plans/2026-07-04-login-time-role-selection.md`. Open follow-ups below.

#### RC-005: Redraw flow diagram for login-time flow
`docs/rolechooser-flow.excalidraw` still shows the mid-session switch. Redraw for the
login-time interstitial flow. Excalidraw MCP canvas at localhost:3000.

#### RC-006: Role filter is per-user, not per-session
`RoleFilterAccessor` keys `IActiveRoleFilter` by user ID, so a second concurrent login by the
same user re-runs `Initialize` and re-widens a narrowed first session back to full roles. Not
escalation (user owns the roles) but makes narrowing unreliable for multi-session users. Fix
direction: key by (user, session/circuit) — needs a session identity that survives Blazor
Server async boundaries.

## Phase 1: Core Module — COMPLETE
- [x] Create `RoleChooser` project (net8.0, DevExpress.ExpressApp + Security refs)
- [x] Add to solution `XafRoleChooser.slnx`
- [x] Implement `IActiveRoleFilter` interface
- [x] Implement `ActiveRoleFilter` (scoped, stores active role IDs, optional logger)
- [x] Implement `ActiveRoleSelection` NonPersistent BO (role name, IsActive checkbox)
- [x] Implement `RoleChooserWindowController` (WindowController, PopupWindowShowAction, multi-select popup)
- [x] Implement security filter via `RoleChooserUserBase` + `RoleFilterAccessor` (ConcurrentDictionary)
- [x] Implement `RoleChooserModule` class (login hook, NoCache warning, type export)
- [x] Create `AddRoleChooser()` service extension for DI registration

## Phase 2: Demo App Integration — COMPLETE
- [x] Update `ApplicationUser` to inherit from `RoleChooserUserBase`
- [x] Register module in Blazor `Startup.cs` (services + module)
- [x] Register module in Win `Startup.cs` (services + module)
- [x] Add project references from Blazor.Server and Win projects
- [x] Add test roles: HR Manager, Project Manager, Sales, Finance
- [x] Add MultiRole test user with all roles
- [x] Add Default role to Admin user

## Phase 3: Infrastructure — COMPLETE
- [x] Docker Compose with SQL Server 2022
- [x] Playwright test project scaffold (17 tests across 3 files)
- [x] Serilog structured logging (Console + File sinks)

## Phase 4: Demo Business Entities — COMPLETE
- [x] Company (CRM group), Employee (HR), Project (Projects), Order/OrderLine (Sales), Invoice (Finance)
- [x] OrderLine split into separate file with `[DefaultClassOptions]` and `[NavigationItem("Sales")]`
- [x] Role-based permissions (HR Manager -> HR, Sales -> Sales/Projects, Finance -> Finance/Sales)
- [x] Finance role has OrderLine_ListView nav permission
- [x] Realistic seed data in Updater.cs
- [x] Company nav group renamed to CRM (entity name conflicted with XAF nav group)

## Phase 5: Documentation — COMPLETE
- [x] Write `docs/how-to-implement.md` — step-by-step guide for XAF developers
- [x] Update `CLAUDE.md` with new module structure
- [x] Design doc at `docs/plans/2026-03-10-role-chooser-design.md`

## Phase 6: Verification — COMPLETE
- [x] Run the Blazor app against Docker SQL Server and verify end-to-end
- [x] Run Playwright tests and fix any selector/timing issues (17/17 pass)
- [x] Fix role loading bug: replaced `AllRoles` property with `GetAllRoles()` method using raw SQL
- [x] Fix inline editing bug: XAF Blazor renders booleans as display-only SVGs in popups — switched to row selection
- [x] Fix security bug: close all open tabs on role switch to prevent unauthorized access
- [x] Tab closing works on both platforms: Blazor (BlazorWindow.Close() via MdiChildWindows), WinForms (ShowViewStrategy.Inspectors)
- [x] Navigation tree rebuild + startup item navigation after role switch
- [x] Verify WinForms app works with the module
- [x] ActiveRoleFilter: logger optional, debug logging removed from IsRoleActive
- [x] AlwaysActiveRoleName cached on IActiveRoleFilter interface
- [x] ApplicationUser has [DefaultClassOptions] for Admin nav visibility

## Resolved Questions
- Security interception: Override `PermissionPolicyUser.Roles` (virtual) via `RoleChooserUserBase` + `RoleFilterAccessor` (ConcurrentDictionary). No XAF API exists to intercept between role loading and permission evaluation.
- Platform-specific projects: Not needed — `WindowController` works cross-platform.
- Service registration: Via `AddRoleChooser()` extension method (XAF `ModuleBase` has no `ConfigureServices` override).
- Popup boolean editing: XAF Blazor popup ListViews render booleans as display-only SVGs. Use row selection (`PopupWindowViewSelectedObjects`) instead.
- Blazor async context: AsyncLocal fails across Blazor Server async boundaries. Use ConcurrentDictionary keyed by user ID.
- Company nav group: Renamed to CRM to avoid XAF entity name = nav group name conflict with IsAdministrative roles.

## Future: XafNavigationHub Integration

When combining RoleChooser with [XafNavigationHub](C:\Projects\XafNavigatonHub):
- [ ] Exclude the hub tab from `CloseAllTabs()` (or navigate back to it) — RoleChooser's tab closing bypasses NavigationHub's `HubTabController` close prevention
- [ ] Verify hub cards refresh after role switch — if the hub is the startup item, navigating to it post-switch should re-read permissions automatically
