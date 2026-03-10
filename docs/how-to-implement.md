# How to Implement the RoleChooser Module in Your XAF Application

## Overview

The RoleChooser module lets users choose which of their assigned roles are active after login, via a toolbar dropdown. Key characteristics:

- A **"Default" role** is always active and cannot be deactivated. All other assigned roles are optional.
- **No restart or re-login needed** — permissions update live when the user changes their active roles.
- Works with both **Blazor Server** and **WinForms** platforms.

This is useful when users have multiple roles (e.g., Manager, DataEntry, Reports) and want to temporarily narrow their effective permissions without logging out.

## Prerequisites

- **DevExpress XAF v25.2+** with EF Core
- **`PermissionsReloadMode.NoCache`** must be configured. The module logs a warning at startup if this is not set.
- Your **User type must inherit from `PermissionPolicyUser`** (standard XAF security), which you will change to inherit from `RoleChooserUserBase` during integration.

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

`RoleChooserUserBase` inherits from `PermissionPolicyUser` and overrides the `Roles` property to return only the currently active roles. The security system reads this property when evaluating permissions, so filtering it is all that is needed to change effective permissions on the fly.

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

The RoleChooser module checks this setting at startup and logs a warning if it detects a different value.

## Configuration

### AlwaysActiveRoleName

By default, the role named **"Default"** is always active and hidden from the chooser UI. You can change this name during module registration:

```csharp
.Add<RoleChooserModule>(m => m.AlwaysActiveRoleName = "MyBaseRole");
```

The always-active role is never shown in the role chooser popup since the user cannot deactivate it.

## How It Works (Technical Details)

1. **`RoleChooserUserBase`** overrides the virtual `Roles` property on `PermissionPolicyUser` to return only the roles that are currently marked as active.
2. **`RoleFilterAccessor`** uses a `ConcurrentDictionary<Guid, IActiveRoleFilter>` keyed by user ID to provide ambient access to the current filter. This allows the entity (which has no access to DI) to read the filter state. (AsyncLocal does not work reliably in Blazor Server due to async context switching.)
3. **`RoleChooserModule`** hooks into `Application.LoggedOn` to initialize the filter with the user's assigned roles. All non-Default roles start as active.
4. **`RoleChooserWindowController`** adds an "Active Roles" toolbar button (on the Tools tab) that opens a popup ListView. Roles are selected via **row-selection checkboxes** (not inline boolean editing — XAF Blazor renders booleans as display-only SVGs in popup ListViews).
5. When the user clicks **OK/Accept**, the controller reads `PopupWindowViewSelectedObjects` to determine which roles are active, updates `IActiveRoleFilter`, and calls `SecuritySystem.ReloadPermissions()`.
6. The controller then **closes all open tabs** (to prevent access to views the user no longer has permissions for) and **recreates navigation items** so the nav tree reflects the new permissions.
7. With `PermissionsReloadMode.NoCache`, the security system re-reads the (now filtered) `Roles` property, and permissions are recalculated immediately.

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

The button is on the **Tools** tab in the XAF Blazor ribbon (not the Home tab). Ensure the `RoleChooserModule` is registered in your startup configuration (Step 4). Also verify the controller is not being filtered out by any custom controller-filtering logic in your application.

### User sees no roles in the chooser

The always-active role (by default "Default") is intentionally hidden from the chooser UI. If the user only has the Default role assigned, the chooser popup will be empty. This is expected — assign additional roles to the user for them to appear.

### Open tabs close on role switch

This is by design. When the user changes active roles, all open tabs are closed to prevent access to views the user no longer has permissions for. The user is navigated back to the startup item.

### Roles revert after navigation

This is expected behavior with `NoCache` mode — the `Roles` property is re-evaluated each time the security system checks permissions. Verify that `RoleFilterAccessor` has the filter registered for the current user. This happens automatically during the `LoggedOn` event, so it should persist for the lifetime of the session. If it does not, check that `AddRoleChooser()` was called during service registration (Step 3).

## Running the Demo

```bash
# Start SQL Server
docker compose up -d

# Run the Blazor app
dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server

# Test users (all have empty passwords):
# - Admin: has Administrators, HR Manager, Project Manager, Sales, Finance roles
# - User: has Default role only
# - MultiRole: has Default + Administrators, HR Manager, Project Manager, Sales, Finance
```
