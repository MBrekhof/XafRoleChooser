# XAF Role Chooser — Design Document

## Goal

Reusable XAF module that adds a multi-select toolbar dropdown allowing users to choose which of their assigned roles are active for the current session. "Default" role is always active and hidden from the chooser. No restart or re-login required — permissions update live.

## Architecture

Standalone XAF module targeting net8.0. Depends only on `DevExpress.ExpressApp` and `DevExpress.ExpressApp.Security` — no ORM dependency (works with EF Core and XPO).

Works with any `IPermissionPolicyUser` that has a `Roles` collection (standard XAF security).

### Core Mechanism

1. `IActiveRoleFilter` / `ActiveRoleFilter` — scoped service storing active role IDs per session. Initialized on login with only "Default" active.
2. `ActiveRoleSelection` — NonPersistent business object modeling a single role row with a checkbox. Used as the list item in the popup.
3. `RoleChooserController` (WindowController) — adds a toolbar action that opens a popup ListView of `ActiveRoleSelection` objects. On Apply, stores selected role IDs in `IActiveRoleFilter`.
4. `RoleChooserSecurityFilter` — intercepts permission evaluation so only Default + user-selected roles are considered.
5. `XafRoleChooserModule` — module class, self-registers `IActiveRoleFilter` as scoped, exports `ActiveRoleSelection`.

### Data Flow

```
Login → All user roles loaded → IActiveRoleFilter initialized (only "Default" active)
User opens toolbar dropdown → Shows assigned roles (except "Default") as checkboxes
User checks roles, clicks Apply → IActiveRoleFilter updated with selected role IDs
Next data access → SecurityStrategy sees only Default + selected roles
UI refreshes with new permission scope
```

### "Default" Role Handling

Module auto-detects the role named "Default" (configurable via `AlwaysActiveRoleName` property on the module). This role is excluded from the chooser UI and always active.

## Consuming App Requirements

- Add module reference and register in startup
- Must use `PermissionsReloadMode.NoCache` (module logs warning if not set)
- Optionally configure `AlwaysActiveRoleName` (defaults to "Default")

## Project Structure

New reusable module project added alongside existing demo app projects:

- `XafRoleChooser.Module` — platform-agnostic reusable module (new)
- Blazor/Win platform projects only if needed (deferred)
- Existing demo app projects reference the new module

## Decisions

- No role dependencies/hierarchy — each role is an independent toggle (YAGNI)
- No restart needed — `PermissionsReloadMode.NoCache` ensures live permission updates
- Flat project structure — no reorganization into demo/ folder for now
- Platform-specific projects deferred — start with module-only, add Blazor/Win packages only if WindowController approach needs platform customization
