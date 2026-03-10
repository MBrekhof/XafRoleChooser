# TODO — XAF Role Chooser

## Phase 1: Core Module — COMPLETE
- [x] Create `RoleChooser` project (net8.0, DevExpress.ExpressApp + Security refs)
- [x] Add to solution `XafRoleChooser.slnx`
- [x] Implement `IActiveRoleFilter` interface
- [x] Implement `ActiveRoleFilter` (scoped, stores active role IDs)
- [x] Implement `ActiveRoleSelection` NonPersistent BO (role name, IsActive checkbox)
- [x] Implement `RoleChooserWindowController` (WindowController, PopupWindowShowAction, multi-select popup)
- [x] Implement security filter via `RoleChooserUserBase` + `RoleFilterAccessor` (AsyncLocal)
- [x] Implement `RoleChooserModule` class (login hook, NoCache warning, type export)
- [x] Create `AddRoleChooser()` service extension for DI registration

## Phase 2: Demo App Integration — COMPLETE
- [x] Update `ApplicationUser` to inherit from `RoleChooserUserBase`
- [x] Register module in Blazor `Startup.cs` (services + module)
- [x] Register module in Win `Startup.cs` (services + module)
- [x] Add project references from Blazor.Server and Win projects
- [x] Add test roles: Manager, DataEntry, Reports
- [x] Add MultiRole test user with all 5 roles
- [x] Update Admin user with Manager + Reports roles

## Phase 3: Infrastructure — COMPLETE
- [x] Docker Compose with SQL Server 2022
- [x] Playwright test project scaffold (17 tests across 3 files)
- [x] Serilog structured logging (Console + File sinks)

## Phase 4: Demo Business Entities — COMPLETE
- [x] Company, Employee, Project, Order/OrderLine, Invoice entities
- [x] Role-based permissions (HR Manager → HR, Sales → Sales/Projects, Finance → Finance/Sales)
- [x] Realistic seed data in Updater.cs

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
- [x] Navigation tree rebuild + startup item navigation after role switch
- [ ] Verify WinForms app works with the module

## Resolved Questions
- Security interception: Override `PermissionPolicyUser.Roles` (virtual) via `RoleChooserUserBase` + `RoleFilterAccessor` (ConcurrentDictionary). No XAF API exists to intercept between role loading and permission evaluation.
- Platform-specific projects: Not needed — `WindowController` works cross-platform.
- Service registration: Via `AddRoleChooser()` extension method (XAF `ModuleBase` has no `ConfigureServices` override).
- Popup boolean editing: XAF Blazor popup ListViews render booleans as display-only SVGs. Use row selection (`PopupWindowViewSelectedObjects`) instead.
- Blazor async context: AsyncLocal fails across Blazor Server async boundaries. Use ConcurrentDictionary keyed by user ID.
