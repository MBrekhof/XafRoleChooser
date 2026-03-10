# XAF Role Chooser

**A reusable DevExpress XAF module that lets users choose which roles are active after login.**

Built with .NET 8 | DevExpress XAF v25.2 | EF Core | SQL Server

---

## The Problem

In standard XAF security, all roles assigned to a user are always active. Every permission from every role is applied simultaneously, and there is no built-in mechanism for a user to temporarily operate with reduced permissions.

This creates real problems in several scenarios:

- **Users with multiple roles who want to work in a specific capacity.** A user assigned both "Manager" and "Admin" may want to perform day-to-day work as a Manager only, without the elevated access that comes with Admin. There is no way to do this out of the box.

- **Security-conscious environments where elevated roles should only be activated when needed.** This is the same principle behind `sudo` on Unix systems — you operate with normal privileges by default and explicitly escalate only when required. XAF offers no equivalent.

- **Testing and auditing.** When verifying what a user can see or do with a specific combination of roles, the only option today is to modify role assignments in the database, which is disruptive and error-prone. With Role Chooser, a user can toggle roles on and off in real time to verify behavior.

- **Compliance scenarios requiring the principle of least privilege.** Regulatory frameworks (SOX, HIPAA, ISO 27001) often mandate that users operate with the minimum permissions necessary. Always-on roles make this difficult to enforce or demonstrate.

XAF Role Chooser solves all of these by letting users selectively activate their assigned roles after login.

---

## How It Works

### User Experience

1. The user logs in normally. Only the **"Default"** role is active.
2. A toolbar button labeled **"Active Roles"** appears in the main window.
3. Clicking it opens a popup with all assigned roles (except Default) displayed as checkboxes.
4. The user selects which roles to activate and clicks **OK**.
5. Permissions update immediately — no restart or re-login is needed.

### Technical Mechanism

The module works by intercepting the point where XAF reads a user's roles for permission evaluation:

- **`RoleChooserUserBase`** inherits from `PermissionPolicyUser` and overrides the `Roles` property. In EF Core, this property is virtual, which makes the override possible. The overridden getter returns only the roles that are currently marked as active, rather than all assigned roles.

- **`RoleFilterAccessor`** uses `AsyncLocal<IActiveRoleFilter>` to provide thread-safe ambient access to the active role filter. This is necessary because EF Core entities are not created through dependency injection — they have no access to the DI container. `AsyncLocal<T>` flows with the `ExecutionContext` across async boundaries, so the filter is available wherever the entity is materialized.

- **`PermissionsReloadMode.NoCache`** ensures that the security system re-evaluates permissions on every `DbContext` operation rather than caching them for the session. This is what makes role changes take effect immediately without re-login. This is the default mode in XAF, so no configuration is typically needed.

- **`SecuritySystem.ReloadPermissions()`** is called when the user clicks Apply in the role chooser popup. This forces the security system to immediately re-read the (now filtered) roles and recalculate all permissions.

---

## Quick Start

### 1. Add a project reference

```bash
# From your XAF solution directory
dotnet add reference path/to/RoleChooser.csproj
```

When the module is published as a NuGet package, this will become a `dotnet add package` command instead.

### 2. Update your ApplicationUser

Your `ApplicationUser` class needs to inherit from `RoleChooserUserBase` instead of `PermissionPolicyUser`:

```csharp
using RoleChooser.Security;

public class ApplicationUser : RoleChooserUserBase, ISecurityUserWithLoginInfo
{
    // All existing code remains unchanged.
    // RoleChooserUserBase inherits from PermissionPolicyUser,
    // so everything that worked before continues to work.
}
```

### 3. Register services in Startup.cs

```csharp
using RoleChooser;

services.AddRoleChooser();
```

This registers the `IActiveRoleFilter` service and the `RoleFilterAccessor` that makes the filter available to entities.

### 4. Register the module

```csharp
builder.Modules
    .Add<RoleChooserModule>();
```

This adds the window controller, business objects, and module configuration to your XAF application.

### 5. Ensure PermissionsReloadMode.NoCache

This is the default in XAF, so you usually do not need to do anything. If your application explicitly sets a different mode, change it back:

```csharp
((SecurityStrategy)securityStrategy).PermissionsReloadMode = PermissionsReloadMode.NoCache;
```

The module will log a warning at startup if it detects a caching mode that would prevent role changes from taking effect.

---

## Configuration

The module has a single configuration option: the name of the role that is always active and cannot be deactivated.

```csharp
// Change the always-active role name (default: "Default")
.Add<RoleChooserModule>(m => m.AlwaysActiveRoleName = "BaseRole");
```

The always-active role is excluded from the role chooser popup entirely. It is always applied to the user regardless of their selections. This ensures that users always have a baseline set of permissions (typically navigation access and basic read permissions).

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    XAF Application                       │
│                                                         │
│  ┌────────────────────┐   ┌───────────────────────────┐ │
│  │ RoleChooser         │   │ ApplicationUser            │ │
│  │ WindowController    │   │ : RoleChooserUserBase      │ │
│  │                    │   │                           │ │
│  │ [Active Roles] btn │   │ Roles → filtered by ──────┼─┼──► SecurityStrategy
│  │      ↓             │   │        IActiveRoleFilter   │ │   evaluates only
│  │ Popup ListView     │   │ GetAllRoles() → raw SQL   │ │   active roles
│  │      ↓             │   └───────────────────────────┘ │
│  │ IActiveRoleFilter  │◄──── AsyncLocal<T> ─────────────┤
│  └────────────────────┘     RoleFilterAccessor           │
└─────────────────────────────────────────────────────────┘
```

The key insight is the separation between `Roles` (filtered, used by the security system) and `GetAllRoles()` (unfiltered, used by the role chooser UI to show all available roles). `GetAllRoles()` loads roles via raw SQL from the `PermissionPolicyRolePermissionPolicyUser` join table, bypassing the filtered `Roles` navigation property entirely. The `NonPersistentObjectSpace.ObjectsGetting` event is used to populate the popup ListView with `ActiveRoleSelection` objects. The `AsyncLocal<T>` bridge connects the controller layer (which has DI access) to the entity layer (which does not).

---

## Project Structure

```
├── src/RoleChooser/              # Reusable module (THE deliverable)
│   ├── BusinessObjects/          # ActiveRoleSelection (NonPersistent)
│   ├── Controllers/              # RoleChooserWindowController
│   ├── Security/                 # RoleChooserUserBase, RoleFilterAccessor
│   ├── Services/                 # IActiveRoleFilter, ActiveRoleFilter
│   ├── RoleChooserModule.cs      # Module definition
│   └── RoleChooserServiceExtensions.cs
├── XafRoleChooser/               # Demo application
│   ├── XafRoleChooser.Module/    # Shared demo module
│   ├── XafRoleChooser.Blazor.Server/  # Blazor Server frontend
│   └── XafRoleChooser.Win/       # WinForms frontend
├── tests/
│   └── XafRoleChooser.Playwright/  # E2E tests
├── docs/
│   ├── how-to-implement.md       # Integration guide
│   └── plans/                    # Design documents
└── docker-compose.yml            # SQL Server 2022 for dev
```

The `src/RoleChooser/` directory is the reusable module — the actual deliverable of this project. Everything else (the `XafRoleChooser/` demo application, tests, docs) exists to demonstrate and validate the module.

---

## Running the Demo

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/products/docker-desktop/) (for SQL Server)
- DevExpress NuGet feed configured (requires a DevExpress license)

### Steps

```bash
# Start the SQL Server 2022 container
docker compose up -d

# Update the connection string in appsettings.Development.json
# Use the DockerConnectionString value

# Run the Blazor Server app
dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server
```

### Test Users

The demo application seeds the following users. All passwords are empty.

| User | Roles |
|---|---|
| **Admin** | Default + Administrators + Manager + Reports |
| **User** | Default only |
| **MultiRole** | Default + Administrators + Manager + DataEntry + Reports |

Log in as **MultiRole** to get the full experience — you will see all roles available in the role chooser popup and can toggle them to observe how the UI and data access change in real time.

---

## Running Tests

The test suite uses [Playwright for .NET](https://playwright.dev/dotnet/) to run end-to-end tests against the Blazor Server application.

```bash
# Build the test project
dotnet build tests/XafRoleChooser.Playwright

# Install Playwright browsers (first time only)
pwsh tests/XafRoleChooser.Playwright/bin/Debug/net8.0/playwright.ps1 install

# Run tests (requires the Blazor app to be running)
dotnet test tests/XafRoleChooser.Playwright
```

The tests require a running instance of the Blazor Server application and a seeded database. Start the demo application first before running the test suite.

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| Override `Roles` property | This is the only reliable interception point. XAF has no public API to filter roles before permission evaluation. Because EF Core makes navigation properties virtual, the override works cleanly without reflection or patching. |
| `AsyncLocal<T>` accessor | Entities loaded by EF Core are not created through dependency injection — they have no access to the DI container. `AsyncLocal<T>` flows with `ExecutionContext` across async boundaries, making the active role filter available wherever the entity is materialized, on any thread. |
| `PermissionsReloadMode.NoCache` requirement | Ensures permissions are re-read per `DbContext` operation, picking up role filter changes immediately. Without this, the security system would cache permissions from login time and role changes would have no effect until the next login. |
| No role dependencies | YAGNI. Each role is an independent toggle. If a project needs role dependencies (e.g., "activating Manager also activates Employee"), that logic can be layered on top of the module without modifying it. |
| `PopupWindowShowAction` | This is the standard XAF pattern for modal popups. It works identically on both Blazor Server and WinForms without any platform-specific code, which keeps the module cross-platform with zero conditional compilation. |

---

## Requirements

- **.NET 8.0** or later
- **DevExpress XAF v25.2** or later (EF Core data access provider)
- **SQL Server** — LocalDB, Docker, or a remote instance
- **`PermissionsReloadMode.NoCache`** — this is the default in XAF. The module will warn at startup if a different mode is detected.

---

## License

MIT License — see [LICENSE](LICENSE) file.

---

## Contributing

Contributions are welcome. Please open an issue first to discuss proposed changes before submitting a pull request.
