# TODO — XAF Role Chooser

Open work only. Completed items are in `docs/DONE.md`.

## P1: High

#### RC-007: WinForms RoleChooser multi-select parity (ID: 1190)
The login-time "Active Roles" chooser works on WinForms (no longer crashes) but only lets you
select ONE role — the generic `Application.CreateListView` grid defaults to single-row select,
unlike Blazor's checkbox list. `ChooseRolesAction_Execute` also reads the selected ROWS
(`PopupWindowViewSelectedObjects`) rather than the `ActiveRoleSelection.IsActive` column, so even
the visible checkbox column wouldn't drive the result.

Fix in the library: enable grid multi-select / checkbox-row mode on WinForms, or switch the
Execute handler to read `IsActive` instead of row selection.
File: `src/RoleChooser/Controllers/RoleChooserWindowController.cs`.
(Was filed on the XafNavigationHub board as #378; re-filed here on 2026-08-03 and #378 closed as a
duplicate. Renumbered RC-007 to avoid colliding with this repo's completed RC-001.)

## P3: Low

#### RC-008: XafNavigationHub integration follow-ups (ID: 1191)
When combining RoleChooser with [XafNavigationHub](C:\Projects\XafNavigatonHub):
- a) Exclude the hub tab from `CloseAllTabs()` (or navigate back to it) — RoleChooser's tab
  closing bypasses NavigationHub's `HubTabController` close prevention.
- b) Verify hub cards refresh after role switch — if the hub is the startup item, navigating to
  it post-switch should re-read permissions automatically.
