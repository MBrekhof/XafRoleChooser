# DONE — XAF Role Chooser

Completed work, most recent first. Open items live in the root `TODO.md`.

## 2026-07-05

#### WinForms duplicate-tab crash on role selection
`ChooseRolesAction_Execute` re-navigating the startup item opened a second "Main" DashboardView
tab on WinForms TabbedMDI, crashing DocumentManager layout restore on re-logon. WinForms now
raises the new `IActiveRoleFilter.SessionRolesApplied` event instead of re-navigating;
`NavigationHubWinController` refreshes the hub in place. Commit 7dbec09. (ContextBoard Hub #380)

#### RC-006: Resolve active-role filter per session, not per user id
Filter now resolves from the logged-in user's own circuit-scoped `ObjectSpace.ServiceProvider`
instead of a process-wide `ConcurrentDictionary<Guid, IActiveRoleFilter>` keyed by user id, so
concurrent same-account logins no longer clobber each other's narrowing. Residual cross-session
behaviours are documented as accepted limitations in `CLAUDE.md`. Commit 0e2585a.

#### RC-005: Redraw flow diagram for login-time flow
`docs/rolechooser-flow.excalidraw` redrawn for the login-time interstitial (was the mid-session
switch). Commit af5b862.

## 2026-07-04

#### RC-004: Update docs for login-time selection
`docs/how-to-implement.md` and design docs updated for the login-time flow.

#### RC-003: SingleRole seed user + Playwright suite for new flow
Added the SingleRole seed user (Default, Sales — chooser skipped, both active); Playwright suite
updated for the login-time chooser. Commits 16c3fee, 7e8b96a.

#### RC-002: Login-time role chooser interstitial; remove switch machinery
Chooser now fires once at login via `XafApplication.ViewShown` (views never land on the Blazor
MDI main window); mid-session switch machinery removed. Commits 2dbb48f, 285467d.

#### RC-001: Pass-through Roles override when not filtering (ROLE-001 fix)
The `Roles` override is pass-through unless the session was narrowed, fixing the WLNCentral
ROLE-001 write-loss bug (admin editing another user no longer persists a filtered role set).

## Initial implementation (through 2026-07-04)

Core module (`src/RoleChooser/`): `IActiveRoleFilter`/`ActiveRoleFilter`, `ActiveRoleSelection`
NonPersistent BO, `RoleChooserWindowController`, `RoleChooserUserBase`, `RoleChooserModule`, and
the `AddRoleChooser()` DI extension. Demo app integration (Blazor + WinForms), demo business
entities (Company/Employee/Project/Order/OrderLine/Invoice) with role permissions and seed data,
Docker SQL Server 2022, Serilog logging, and the Playwright E2E suite. Row-selection popup (XAF
Blazor renders popup booleans as display-only SVGs), tab-closing on role switch (both platforms),
navigation rebuild, and the CRM nav-group rename. Design rationale lives in `CLAUDE.md`.
