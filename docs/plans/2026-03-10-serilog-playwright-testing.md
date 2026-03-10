# Serilog Logging & Playwright Role-Switching Tests

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add comprehensive Serilog logging to diagnose role switching, fix navigation refresh after role change, and write headed Playwright tests that verify role-based navigation visibility.

**Architecture:** Add Serilog to the Blazor Server host and inject `ILogger<T>` into every RoleChooser component. Fix the post-role-switch navigation refresh by calling `Application.MainWindow.SetView(...)` or navigating to the default URL. Write Playwright tests in headed mode that login as Admin, switch roles, and assert which nav groups appear/disappear.

**Tech Stack:** Serilog (Console + File sinks), Playwright NUnit (headed), XAF Blazor

---

### Task 1: Add Serilog to Blazor Server host

**Files:**
- Modify: `XafRoleChooser/XafRoleChooser.Blazor.Server/XafRoleChooser.Blazor.Server.csproj`
- Modify: `XafRoleChooser/XafRoleChooser.Blazor.Server/Program.cs`
- Modify: `XafRoleChooser/XafRoleChooser.Blazor.Server/appsettings.Development.json`

**Step 1: Add Serilog NuGet packages**

Add to `XafRoleChooser.Blazor.Server.csproj`:
```xml
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
```

Run: `dotnet restore XafRoleChooser.slnx`

**Step 2: Configure Serilog in Program.cs**

Replace `CreateHostBuilder` with Serilog bootstrap:
```csharp
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/xafrolechooser-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"))
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
```

Add using: `using Serilog;`

**Step 3: Add Serilog minimum level override for RoleChooser in appsettings.Development.json**

```json
"Serilog": {
    "MinimumLevel": {
        "Default": "Information",
        "Override": {
            "RoleChooser": "Debug",
            "Microsoft.AspNetCore": "Warning"
        }
    }
}
```

**Step 4: Build and verify**

Run: `dotnet build XafRoleChooser.slnx`
Expected: 0 errors

**Step 5: Commit**

```bash
git add XafRoleChooser/XafRoleChooser.Blazor.Server/
git commit -m "feat: add Serilog with Console + File sinks to Blazor Server host"
```

---

### Task 2: Add logging to RoleChooserModule (LoggedOn, SetupComplete)

**Files:**
- Modify: `src/RoleChooser/RoleChooser.csproj` (add `Microsoft.Extensions.Logging.Abstractions`)
- Modify: `src/RoleChooser/RoleChooserModule.cs`

**Step 1: Add logging abstractions package**

Add to `src/RoleChooser/RoleChooser.csproj`:
```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
```

**Step 2: Add detailed logging to RoleChooserModule**

In `Application_SetupComplete`, log the PermissionsReloadMode.

In `Application_LoggedOn`, log:
- User ID and user name at start
- Each role loaded from raw SQL (role ID + role name)
- Which role is the always-active role
- Total count of available roles
- Confirmation that RoleFilterAccessor.Current has been set

```csharp
private void Application_LoggedOn(object? sender, LogonEventArgs e)
{
    var app = (XafApplication)sender!;
    var logger = app.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserModule>();
    var filter = app.ServiceProvider.GetRequiredService<IActiveRoleFilter>();
    var userId = (Guid)app.Security.UserId;

    logger?.LogInformation("LoggedOn: User {UserId} logged in, loading roles...", userId);

    // ... existing raw SQL code ...
    // Inside the while (reader.Read()) loop:
    logger?.LogDebug("LoggedOn: Found role {RoleName} ({RoleId}) for user {UserId}", roleName, roleId, userId);

    // After Initialize:
    logger?.LogInformation("LoggedOn: Initialized filter — AlwaysActive={AlwaysActiveRoleId}, AvailableRoles={Count}: [{RoleNames}]",
        alwaysActiveRoleId, availableRoles.Count,
        string.Join(", ", availableRoles.Select(r => r.Name)));

    // After setting accessor:
    logger?.LogInformation("LoggedOn: RoleFilterAccessor.Current set, role filtering is now active");
}
```

**Step 3: Build and verify**

Run: `dotnet build XafRoleChooser.slnx`
Expected: 0 errors

**Step 4: Commit**

```bash
git add src/RoleChooser/
git commit -m "feat: add Serilog logging to RoleChooserModule LoggedOn and SetupComplete"
```

---

### Task 3: Add logging to ActiveRoleFilter

**Files:**
- Modify: `src/RoleChooser/Services/ActiveRoleFilter.cs`

**Step 1: Inject ILogger and log state changes**

```csharp
using Microsoft.Extensions.Logging;

public class ActiveRoleFilter : IActiveRoleFilter
{
    private readonly ILogger<ActiveRoleFilter> _logger;
    private HashSet<Guid> _activeRoleIds = new();
    private List<(Guid Id, string Name)> _availableRoles = new();

    public ActiveRoleFilter(ILogger<ActiveRoleFilter> logger)
    {
        _logger = logger;
    }

    public void Initialize(Guid? alwaysActiveRoleId, IEnumerable<(Guid Id, string Name)> availableRoles)
    {
        AlwaysActiveRoleId = alwaysActiveRoleId;
        _availableRoles = availableRoles.ToList();
        _activeRoleIds = new HashSet<Guid>(_availableRoles.Select(r => r.Id));
        _logger.LogInformation("Initialize: AlwaysActive={AlwaysActiveId}, Activated {Count} roles: [{Roles}]",
            alwaysActiveRoleId, _activeRoleIds.Count,
            string.Join(", ", _availableRoles.Select(r => r.Name)));
    }

    public void SetActiveRoles(IEnumerable<Guid> roleIds)
    {
        _activeRoleIds = new HashSet<Guid>(roleIds);
        var activeNames = _availableRoles
            .Where(r => _activeRoleIds.Contains(r.Id))
            .Select(r => r.Name);
        _logger.LogInformation("SetActiveRoles: Now active ({Count}): [{Roles}]",
            _activeRoleIds.Count, string.Join(", ", activeNames));
    }

    public bool IsRoleActive(Guid roleId)
    {
        if (AlwaysActiveRoleId.HasValue && roleId == AlwaysActiveRoleId.Value)
            return true;
        var active = _activeRoleIds.Contains(roleId);
        _logger.LogDebug("IsRoleActive: {RoleId} => {Active}", roleId, active);
        return active;
    }
}
```

**Step 2: Build and verify**

Run: `dotnet build XafRoleChooser.slnx`

**Step 3: Commit**

```bash
git add src/RoleChooser/Services/ActiveRoleFilter.cs
git commit -m "feat: add logging to ActiveRoleFilter state changes"
```

---

### Task 4: Add logging to RoleChooserWindowController

**Files:**
- Modify: `src/RoleChooser/Controllers/RoleChooserWindowController.cs`

**Step 1: Add logging to popup open and Execute**

Log:
- When popup is being prepared, list all roles and their current IsActive state
- When Execute fires, list selected role IDs and names
- Whether ReloadPermissions was called
- Whether view refresh happened

```csharp
private void ChooseRolesAction_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
{
    var logger = Application.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserWindowController>();
    if (_roleFilter == null)
    {
        logger?.LogWarning("CustomizePopupWindowParams: _roleFilter is null, skipping popup");
        return;
    }

    logger?.LogInformation("CustomizePopupWindowParams: Building popup with {Count} available roles", _roleFilter.AvailableRoles.Count);

    // ... existing code to build items ...
    // After building items list:
    foreach (var item in items)
    {
        logger?.LogDebug("  Role: {RoleName} ({RoleId}) IsActive={IsActive}", item.RoleName, item.RoleId, item.IsActive);
    }
    // ... rest of existing code ...
}

private void ChooseRolesAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
{
    var logger = Application.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserWindowController>();
    // ... collect selectedRoleIds ...
    logger?.LogInformation("Execute: User selected {Count} roles: [{RoleIds}]", selectedRoleIds.Count,
        string.Join(", ", selectedRoleIds));

    _roleFilter.SetActiveRoles(selectedRoleIds);

    if (Application.Security is ISecurityStrategyBase securityStrategy)
    {
        securityStrategy.ReloadPermissions();
        logger?.LogInformation("Execute: ReloadPermissions() called successfully");
    }
    else
    {
        logger?.LogWarning("Execute: Application.Security is NOT ISecurityStrategyBase — permissions NOT reloaded!");
    }
}
```

**Step 2: Build and verify**

Run: `dotnet build XafRoleChooser.slnx`

**Step 3: Commit**

```bash
git add src/RoleChooser/Controllers/RoleChooserWindowController.cs
git commit -m "feat: add logging to RoleChooserWindowController popup and execute"
```

---

### Task 5: Add logging to RoleChooserUserBase.Roles override

**Files:**
- Modify: `src/RoleChooser/Security/RoleChooserUserBase.cs`

**Step 1: Add logging to the Roles property getter**

Since this is an entity (no DI), use a static logger pattern:

```csharp
using Microsoft.Extensions.Logging;

public abstract class RoleChooserUserBase : PermissionPolicyUser
{
    private static ILogger? _logger;

    /// <summary>
    /// Call once at startup to enable logging for the Roles override.
    /// </summary>
    public static void SetLogger(ILoggerFactory? loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger("RoleChooser.RoleChooserUserBase");
    }

    public override IList<PermissionPolicyRole> Roles
    {
        get
        {
            var allRoles = base.Roles;
            var filter = RoleFilterAccessor.Current;
            if (filter == null || allRoles is not { Count: > 0 })
            {
                _logger?.LogDebug("Roles: No filter or empty roles, returning {Count} unfiltered roles", allRoles?.Count ?? 0);
                return allRoles;
            }

            var filtered = allRoles.Where(r => filter.IsRoleActive(r.ID)).ToList();
            _logger?.LogDebug("Roles: Filtered {AllCount} → {FilteredCount} active roles: [{Names}]",
                allRoles.Count, filtered.Count,
                string.Join(", ", filtered.Select(r => r.Name)));
            return filtered;
        }
        set => base.Roles = value;
    }
}
```

Call `RoleChooserUserBase.SetLogger(loggerFactory)` from `RoleChooserModule.Application_SetupComplete`.

**Step 2: Build and verify**

Run: `dotnet build XafRoleChooser.slnx`

**Step 3: Commit**

```bash
git add src/RoleChooser/Security/RoleChooserUserBase.cs src/RoleChooser/RoleChooserModule.cs
git commit -m "feat: add logging to RoleChooserUserBase.Roles filtering"
```

---

### Task 6: Fix navigation refresh after role switch

**Files:**
- Modify: `src/RoleChooser/Controllers/RoleChooserWindowController.cs`

**Step 1: Force full navigation rebuild after ReloadPermissions**

After calling `ReloadPermissions()`, the Blazor navigation tree is stale. The XAF way to rebuild it in Blazor is to navigate to the default view:

```csharp
private void ChooseRolesAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
{
    // ... existing code collecting selectedRoleIds and calling SetActiveRoles ...

    if (Application.Security is ISecurityStrategyBase securityStrategy)
    {
        securityStrategy.ReloadPermissions();
        logger?.LogInformation("Execute: ReloadPermissions() called successfully");
    }

    // Force navigation rebuild by navigating to the startup view
    // This causes XAF Blazor to re-evaluate navigation permissions
    if (Application.MainWindow != null)
    {
        var startupViewId = Application.GetStartupNavigationItem()?.ViewID;
        if (!string.IsNullOrEmpty(startupViewId))
        {
            var shortcut = new ViewShortcut(startupViewId, null);
            Application.MainWindow.SetView(Application.ProcessShortcut(shortcut));
            logger?.LogInformation("Execute: Navigated to startup view {ViewId} to rebuild navigation", startupViewId);
        }
    }
}
```

**NOTE:** `Application.GetStartupNavigationItem()` may not exist — check the XAF API. Alternative: use `Application.ShowViewStrategy.ShowStartupWindow()` or `Frame.SetView(Application.CreateDashboardView(...))`. The exact API depends on XAF version. If unavailable, use a simpler approach: `Application.MainWindow.SetView(Application.CreateDashboardView(Application.CreateObjectSpace(), "Main", true))` or navigate to the default start page. We should test what works and log the result.

**Step 2: Build and verify**

Run: `dotnet build XafRoleChooser.slnx`

**Step 3: Start the app, login as Admin, switch roles, check logs**

Run: `dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server/XafRoleChooser.Blazor.Server.csproj`

Check console output for RoleChooser log entries. Verify navigation changes when deactivating Administrators.

**Step 4: Commit**

```bash
git add src/RoleChooser/Controllers/RoleChooserWindowController.cs
git commit -m "fix: force navigation rebuild after role switch in XAF Blazor"
```

---

### Task 7: Write Playwright tests — infrastructure updates

**Files:**
- Modify: `tests/XafRoleChooser.Playwright/Infrastructure/TestConstants.cs`
- Modify: `tests/XafRoleChooser.Playwright/PageObjects/MainPage.cs`
- Create: `tests/XafRoleChooser.Playwright/.runsettings`

**Step 1: Add headed mode .runsettings**

Create `tests/XafRoleChooser.Playwright/.runsettings`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <Playwright>
    <BrowserName>chromium</BrowserName>
    <LaunchOptions>
      <Headless>false</Headless>
      <SlowMo>500</SlowMo>
    </LaunchOptions>
  </Playwright>
</RunSettings>
```

**Step 2: Add MultiRole user constant and role name constants**

Update `TestConstants.cs`:
```csharp
public static class TestConstants
{
    public const string BaseUrl = "https://localhost:5001";
    public const string AdminUser = "Admin";
    public const string MultiRoleUser = "MultiRole";
    public const string RegularUser = "User";
    public const string EmptyPassword = "";
    public const int DefaultTimeout = 30000;

    // Role names
    public const string RoleAdministrators = "Administrators";
    public const string RoleHRManager = "HR Manager";
    public const string RoleProjectManager = "Project Manager";
    public const string RoleSales = "Sales";
    public const string RoleFinance = "Finance";

    // Navigation groups (text visible in sidebar)
    public const string NavCompany = "Company";
    public const string NavHR = "HR";
    public const string NavProjects = "Projects";
    public const string NavSales = "Sales";
    public const string NavFinance = "Finance";
}
```

**Step 3: Update MainPage with robust nav-group checking methods**

Add to `MainPage.cs`:
```csharp
public async Task<bool> IsNavGroupVisible(string groupName, int timeoutMs = 3000)
{
    try
    {
        var navGroup = Page.Locator($".nav-item:has-text('{groupName}'), .xaf-navigation-item:has-text('{groupName}'), [data-nav-item-text='{groupName}']");
        await navGroup.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        return true;
    }
    catch (TimeoutException)
    {
        return false;
    }
}

public async Task<bool> IsNavGroupHidden(string groupName, int timeoutMs = 3000)
{
    try
    {
        var navGroup = Page.Locator($".nav-item:has-text('{groupName}'), .xaf-navigation-item:has-text('{groupName}'), [data-nav-item-text='{groupName}']");
        await navGroup.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = timeoutMs });
        return true;
    }
    catch (TimeoutException)
    {
        return false;
    }
}

public async Task WaitForNavigationRefresh()
{
    // After role switch, wait for page to reload/navigation to rebuild
    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    await WaitForXafReady();
    // Extra wait for Blazor re-render
    await Page.WaitForTimeoutAsync(1000);
}

public async Task SetRolesAndAccept(params string[] roleNamesToActivate)
{
    await ClickActiveRolesButton();

    // Get all role rows in the popup and uncheck all, then check only the requested ones
    var allItems = Page.Locator(".dxbs-grid-row, tr").Filter(new() { HasText = "Active" }).Or(
        Page.Locator(".dxbl-grid-row"));

    // For each available role, check if it needs to be toggled
    foreach (var (id, name) in await GetPopupRoleStates())
    {
        bool shouldBeActive = roleNamesToActivate.Contains(name);
        bool isCurrentlyActive = id; // id repurposed as bool here — see actual implementation
        if (shouldBeActive != isCurrentlyActive)
        {
            await ToggleRoleInChooser(name);
        }
    }

    await AcceptRoleChooser();
    await WaitForNavigationRefresh();
}
```

Note: The `SetRolesAndAccept` helper is aspirational — the actual implementation depends on what checkbox selectors work. The tests below will use `ToggleRoleInChooser` directly and handle the state explicitly.

**Step 4: Build and verify**

Run: `dotnet build XafRoleChooser.slnx`

**Step 5: Commit**

```bash
git add tests/XafRoleChooser.Playwright/
git commit -m "feat: add Playwright infrastructure for headed role-switching tests"
```

---

### Task 8: Write Playwright test — HR Manager only

**Files:**
- Create: `tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs`

**Step 1: Write the test**

```csharp
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using XafRoleChooser.Playwright.Infrastructure;
using XafRoleChooser.Playwright.PageObjects;

namespace XafRoleChooser.Playwright.Tests;

[TestFixture]
public class RoleSwitchingTests : PageTest
{
    private LoginPage _loginPage = null!;
    private MainPage _mainPage = null!;

    [SetUp]
    public async Task SetUp()
    {
        _loginPage = new LoginPage(Page);
        _mainPage = new MainPage(Page);
    }

    [Test]
    public async Task Admin_WithOnlyHRManager_ShouldSeeOnlyHRAndCompany()
    {
        // Login as Admin (has all roles)
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        // Open role chooser
        await _mainPage.ClickActiveRolesButton();

        // Deactivate all except HR Manager:
        // Uncheck Administrators, Project Manager, Sales, Finance
        // Keep HR Manager checked
        await _mainPage.ToggleRoleInChooser(TestConstants.RoleAdministrators);
        await _mainPage.ToggleRoleInChooser(TestConstants.RoleProjectManager);
        await _mainPage.ToggleRoleInChooser(TestConstants.RoleSales);
        await _mainPage.ToggleRoleInChooser(TestConstants.RoleFinance);

        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForNavigationRefresh();

        // HR Manager should see: HR group, Company group
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True,
            "HR nav group should be visible with HR Manager role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True,
            "Company nav group should be visible with HR Manager role");

        // Should NOT see: Projects, Sales, Finance
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavProjects), Is.True,
            "Projects nav group should be hidden without Project Manager role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavSales), Is.True,
            "Sales nav group should be hidden without Sales role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
            "Finance nav group should be hidden without Finance role");
    }
}
```

**Step 2: Run test in headed mode to verify**

Run: `dotnet test tests/XafRoleChooser.Playwright/ --filter "Admin_WithOnlyHRManager" --settings tests/XafRoleChooser.Playwright/.runsettings -v n`

Watch the browser — verify the popup opens, roles are toggled, and navigation changes.

**Step 3: Commit**

```bash
git add tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs
git commit -m "test: add Playwright test — HR Manager only shows HR + Company nav"
```

---

### Task 9: Write Playwright test — Sales only

**Files:**
- Modify: `tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs`

**Step 1: Add test**

```csharp
[Test]
public async Task Admin_WithOnlySales_ShouldSeeSalesCompanyProjects()
{
    await _loginPage.NavigateTo();
    await _loginPage.Login(TestConstants.AdminUser);
    await _mainPage.WaitForXafReady();

    await _mainPage.ClickActiveRolesButton();

    // Uncheck all except Sales
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleAdministrators);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleHRManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleProjectManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleFinance);

    await _mainPage.AcceptRoleChooser();
    await _mainPage.WaitForNavigationRefresh();

    // Sales role sees: Sales, Company, Projects (read-only)
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True,
        "Sales nav group should be visible");
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True,
        "Company nav group should be visible");
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavProjects), Is.True,
        "Projects nav group should be visible (Sales has read on Projects)");

    // Should NOT see: HR, Finance
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavHR), Is.True,
        "HR nav group should be hidden without HR Manager role");
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
        "Finance nav group should be hidden without Finance role");
}
```

**Step 2: Run and verify**

Run: `dotnet test tests/XafRoleChooser.Playwright/ --filter "Admin_WithOnlySales" --settings tests/XafRoleChooser.Playwright/.runsettings -v n`

**Step 3: Commit**

```bash
git add tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs
git commit -m "test: add Playwright test — Sales only shows Sales + Company + Projects nav"
```

---

### Task 10: Write Playwright test — Finance only

**Files:**
- Modify: `tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs`

**Step 1: Add test**

```csharp
[Test]
public async Task Admin_WithOnlyFinance_ShouldSeeFinanceSalesCompany()
{
    await _loginPage.NavigateTo();
    await _loginPage.Login(TestConstants.AdminUser);
    await _mainPage.WaitForXafReady();

    await _mainPage.ClickActiveRolesButton();

    // Uncheck all except Finance
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleAdministrators);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleHRManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleProjectManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleSales);

    await _mainPage.AcceptRoleChooser();
    await _mainPage.WaitForNavigationRefresh();

    // Finance role sees: Finance, Sales (read-only), Company
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavFinance), Is.True,
        "Finance nav group should be visible");
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True,
        "Sales nav group should be visible (Finance has read on Orders)");
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True,
        "Company nav group should be visible");

    // Should NOT see: HR, Projects
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavHR), Is.True,
        "HR nav group should be hidden");
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavProjects), Is.True,
        "Projects nav group should be hidden");
}
```

**Step 2: Run and verify**

Run: `dotnet test tests/XafRoleChooser.Playwright/ --filter "Admin_WithOnlyFinance" --settings tests/XafRoleChooser.Playwright/.runsettings -v n`

**Step 3: Commit**

```bash
git add tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs
git commit -m "test: add Playwright test — Finance only shows Finance + Sales + Company nav"
```

---

### Task 11: Write Playwright test — Reactivate Administrators sees everything

**Files:**
- Modify: `tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs`

**Step 1: Add test**

```csharp
[Test]
public async Task Admin_ReactivateAdministrators_ShouldSeeEverything()
{
    await _loginPage.NavigateTo();
    await _loginPage.Login(TestConstants.AdminUser);
    await _mainPage.WaitForXafReady();

    // First, deactivate Administrators
    await _mainPage.ClickActiveRolesButton();
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleAdministrators);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleHRManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleProjectManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleSales);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleFinance);
    await _mainPage.AcceptRoleChooser();
    await _mainPage.WaitForNavigationRefresh();

    // Now reactivate only Administrators
    await _mainPage.ClickActiveRolesButton();
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleAdministrators);
    await _mainPage.AcceptRoleChooser();
    await _mainPage.WaitForNavigationRefresh();

    // Should see everything
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True);
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True);
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavProjects), Is.True);
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True);
    Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavFinance), Is.True);
}
```

**Step 2: Run and verify**

Run: `dotnet test tests/XafRoleChooser.Playwright/ --filter "Admin_ReactivateAdministrators" --settings tests/XafRoleChooser.Playwright/.runsettings -v n`

**Step 3: Commit**

```bash
git add tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs
git commit -m "test: add Playwright test — reactivate Administrators restores all nav"
```

---

### Task 12: Write Playwright test — Deactivate all roles (Default only)

**Files:**
- Modify: `tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs`

**Step 1: Add test**

```csharp
[Test]
public async Task Admin_DeactivateAllRoles_ShouldSeeOnlyDefault()
{
    await _loginPage.NavigateTo();
    await _loginPage.Login(TestConstants.AdminUser);
    await _mainPage.WaitForXafReady();

    // Deactivate ALL roles (Default is always active, not shown in chooser)
    await _mainPage.ClickActiveRolesButton();
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleAdministrators);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleHRManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleProjectManager);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleSales);
    await _mainPage.ToggleRoleInChooser(TestConstants.RoleFinance);
    await _mainPage.AcceptRoleChooser();
    await _mainPage.WaitForNavigationRefresh();

    // With only Default role, should see none of the business nav groups
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavCompany), Is.True,
        "Company nav should be hidden with Default role only");
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavHR), Is.True,
        "HR nav should be hidden with Default role only");
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavProjects), Is.True,
        "Projects nav should be hidden with Default role only");
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavSales), Is.True,
        "Sales nav should be hidden with Default role only");
    Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
        "Finance nav should be hidden with Default role only");
}
```

**Step 2: Run and verify**

Run: `dotnet test tests/XafRoleChooser.Playwright/ --filter "Admin_DeactivateAllRoles" --settings tests/XafRoleChooser.Playwright/.runsettings -v n`

**Step 3: Commit**

```bash
git add tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs
git commit -m "test: add Playwright test — deactivate all shows only Default (no business nav)"
```

---

## Important Notes for Implementation

1. **Navigation selectors will need tuning.** XAF Blazor generates its own navigation markup. Run the first test in headed mode, inspect the DOM to find the actual selectors for nav groups. Update `MainPage.IsNavGroupVisible/Hidden` accordingly.

2. **The navigation refresh approach (Task 6) may need iteration.** The XAF Blazor API for forcing navigation rebuild varies by version. Check DevExpress docs for `ShowViewStrategy` and `MainWindow.SetView`. Log every step so we can see what's happening.

3. **Headed mode is for debugging.** The `.runsettings` file with `Headless=false` and `SlowMo=500` is for visual debugging. For CI, remove the `.runsettings` or set `Headless=true`.

4. **Admin user has no Default role.** Looking at the Updater, Admin gets: Administrators, HR Manager, Project Manager, Sales, Finance — but NOT Default. This means deactivating all roles leaves Admin with zero active roles (no Default). Consider whether to add Default to Admin or handle this in tests.

5. **Tests assume all roles start active after login.** This is correct per `ActiveRoleFilter.Initialize` which activates all available roles by default.
