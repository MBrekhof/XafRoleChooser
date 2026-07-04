# How to Implement the RoleChooser Module in Your XAF Application

## Overview

The RoleChooser module lets users choose which of their assigned roles are active for the session, via a one-time popup shown right after login. Key characteristics:

- A **"Default" role** is always active and cannot be deactivated. All other assigned roles are optional.
- **Roles are chosen once, right after login; changing them requires re-login.** The chooser is skipped when the user has fewer than two optional roles — all assigned roles are active in that case.
- Works with both **Blazor Server** and **WinForms** platforms.

This is useful when users have multiple roles (e.g., Manager, DataEntry, Reports) and want to start a session with a deliberately narrowed set of permissions instead of always operating with the union of every assigned role.

## Prerequisites

- **DevExpress XAF v25.2+** with EF Core
- **`PermissionsReloadMode.NoCache`** must be configured. The module logs a warning at startup if this is not set.
- Your **User type must inherit from `PermissionPolicyUser`** (standard XAF security), which you will change to inherit from `RoleChooserUserBase` during integration.
- **Every user must be assigned the always-active role** (e.g. "Default"). The module does not validate this — a user without it can end up with no active permissions at all.

## Step-by-Step Integration

### Step 1: Add the RoleChooser Project Reference

Add a project reference to the RoleChooser library:

```bash
dotnet add reference path/to/RoleChooser.csproj
```

Or, when published as a NuGet package:

```bash
dotnet add package RoleChooser
```

### Step 2: Update Your ApplicationUser Class

Change the base class of your `ApplicationUser` from `PermissionPolicyUser` to `RoleChooserUserBase`:

```csharp
// Before:
public class ApplicationUser : PermissionPolicyUser, ISecurityUserWithLoginInfo

// After:
using RoleChooser.Security;

public class ApplicationUser : RoleChooserUserBase, ISecurityUserWithLoginInfo
```

`RoleChooserUserBase` inherits from `PermissionPolicyUser` and overrides the `Roles` property to return only the currently active roles (once the user has narrowed their session selection — see "How It Works" for the pass-through behavior otherwise). The security system reads this property when evaluating permissions, so filtering it is all that is needed to change effective permissions for the session.

`RoleChooserUserBase` also provides a `GetAllRoles()` method that returns every role assigned to the user, regardless of active/inactive state. This method loads roles via raw SQL from the `PermissionPolicyRolePermissionPolicyUser` join table, bypassing the filtered `Roles` property. Use `GetAllRoles()` whenever you need the full list (e.g., in admin views, the role chooser UI, or audit logic).

### Step 3: Register Services

**Blazor Server** — in your `Startup.cs`:

```csharp
using RoleChooser;

// In ConfigureServices:
services.AddRoleChooser();
```

**WinForms** — in your `Startup.cs`:

```csharp
using RoleChooser;

builder.Services.AddRoleChooser();
```

### Step 4: Register the Module

**Blazor Server** — in your `Startup.cs` module registration:

```csharp
builder.Modules
    // ... other modules
    .Add<RoleChooserModule>();
```

**WinForms** — in your `Startup.cs`:

```csharp
builder.Modules
    // ... other modules
    .Add<RoleChooserModule>();
```

### Step 5: Ensure PermissionsReloadMode.NoCache

Verify that your security strategy uses `NoCache` mode. This is the default in XAF, but if your project overrides it, the module will not work correctly.

```csharp
options.Events.OnSecurityStrategyCreated += securityStrategy =>
{
    ((SecurityStrategy)securityStrategy).PermissionsReloadMode = PermissionsReloadMode.NoCache;
};
```

The RoleChooser module checks this setting at startup and logs a warning if it detects a different value. This is still required with the login-time selection flow: without `NoCache`, the `ReloadPermissions()` call the module makes when the user accepts the chooser has no effect, and the user keeps the full pre-selection permission set for the rest of the session.

## Configuration

### AlwaysActiveRoleName

By default, the role named **"Default"** is always active and hidden from the chooser UI. You can change this name during module registration:

```csharp
.Add<RoleChooserModule>(m => m.AlwaysActiveRoleName = "MyBaseRole");
```

The always-active role is never shown in the role chooser popup since the user cannot deactivate it.

## How It Works (Technical Details)

1. **`RoleChooserUserBase`** overrides the virtual `Roles` property on `PermissionPolicyUser`. It is a **pass-through** to `base.Roles` (the live, tracked collection) unless the session was actually narrowed — i.e. the user deselected at least one optional role. Only then does it return a filtered, detached snapshot. This matters because it's why role assignment edits (Link/Unlink) on the User DetailView persist normally in an all-roles session but silently don't in a narrowed one.
2. **`RoleFilterAccessor`** uses a `ConcurrentDictionary<Guid, IActiveRoleFilter>` keyed by user ID to provide ambient access to the current filter. This allows the entity (which has no access to DI) to read the filter state. (AsyncLocal does not work reliably in Blazor Server due to async context switching.)
3. **`RoleChooserModule`** hooks into `Application.LoggedOn` to initialize the filter with the user's assigned roles. All non-Default roles start as active.
4. **`RoleChooserWindowController`** subscribes to `XafApplication.ViewShown` when it activates on the main window, and shows the chooser popup automatically the first time a view is shown after login — then unsubscribes, so it never shows again for that session. (`Window.ViewChanged` is the wrong signal here: in XAF Blazor's tabbed MDI, views land on MDI child windows, never on the main window itself.) If the user has fewer than two optional roles, the popup is skipped entirely and all roles stay active.
5. The popup ListView uses **row-selection checkboxes** (not inline boolean editing — XAF Blazor renders booleans as display-only SVGs in popup ListViews). When the user clicks **Accept**, the controller reads `PopupWindowViewSelectedObjects` to determine which roles are active, updates `IActiveRoleFilter`, and calls `SecuritySystem.ReloadPermissions()`. Clicking **Cancel** keeps all roles active (no permission change, so no reload is needed).
6. Either way, the controller then **recreates navigation items** so the nav tree reflects the applied roles, and navigates to the startup view. There are no open tabs to close at this point — the chooser runs before the user has had a chance to open anything.
7. The role selection is final for the session: once the chooser has run its course (Accept, Cancel, or an automatic skip), the "Active Roles" Tools action goes inactive, and choosing a different combination of roles requires logging out and back in.

## Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│                   XAF Application               │
│                                                 │
│  ┌──────────────────┐  ┌─────────────────────┐  │
│  │ RoleChooser      │  │ ApplicationUser     │  │
│  │ WindowController │  │ : RoleChooserUserBase│  │
│  │                  │  │                     │  │
│  │ [Active Roles]   │  │ Roles (filtered) ──►│──┼── SecurityStrategy
│  │  ↓               │  │ GetAllRoles() (all) │  │   reads only active
│  │ PopupListView    │  └─────────────────────┘  │   roles for permission
│  │  ↓               │                           │   evaluation
│  │ IActiveRoleFilter│◄──── AsyncLocal ──────────┤
│  └──────────────────┘    RoleFilterAccessor      │
└─────────────────────────────────────────────────┘
```

## Troubleshooting

### Role changes don't take effect

Verify that `PermissionsReloadMode.NoCache` is set on your security strategy. Check the application log output for a warning from `RoleChooserModule` — it explicitly warns if the mode is not configured correctly.

### "Active Roles" button doesn't appear

The button is on the **Tools** tab in the XAF Blazor ribbon (not the Home tab). Ensure the `RoleChooserModule` is registered in your startup configuration (Step 4). Also verify the controller is not being filtered out by any custom controller-filtering logic in your application. Note that the button is expected to go **inactive** once the session's role selection has been made (Accept, Cancel, or automatic skip) — this is by design, not a bug.

### Chooser doesn't appear

The always-active role (by default "Default") is never counted or shown. The popup only appears when the user has **two or more** other assigned roles; with fewer than two, the chooser is skipped by design and all roles are active. If a user has two or more optional roles and the popup still doesn't show, verify that `RoleFilterAccessor` has the filter registered for the current user — this happens automatically during the `LoggedOn` event, so check that `AddRoleChooser()` was called during service registration (Step 3).

### Role assignment from User detail view doesn't save

This is only possible when the current session is a **narrowed** one (the user deselected at least one optional role in the chooser). In a narrowed session, `RoleChooserUserBase.Roles` returns a filtered, detached snapshot rather than the live tracked collection, so edits to role assignment (Link/Unlink on the User DetailView) silently don't persist. Administer role assignments in a session where all roles were kept active (accept with everything selected, or Cancel) — in that case `Roles` passes through the real tracked collection and edits persist normally.

## Running the Demo

```bash
# Start SQL Server
docker compose up -d

# Run the Blazor app
dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server

# Test users (all have empty passwords):
# - Admin: has Default + Administrators, HR Manager, Project Manager, Sales, Finance (chooser appears)
# - MultiRole: has Default + Administrators, HR Manager, Project Manager, Sales, Finance (chooser appears)
# - User: has Default role only (chooser skipped, only Default active)
# - SingleRole: has Default + Sales only (chooser skipped, both active)
```
