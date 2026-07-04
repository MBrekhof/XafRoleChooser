# Login-Time Role Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace mid-session role switching with a one-time role-selection popup shown right after login (skipped when the user has fewer than two optional roles), and make the `Roles` override pass-through when no filtering is active so standard role administration (M2M Link/Unlink) works again.

**Architecture:** The existing popup, `ActiveRoleSelection` objects, `IActiveRoleFilter`, and `RoleFilterAccessor` plumbing are all reused. What changes: (1) `RoleChooserUserBase.Roles` returns the *real tracked collection* unless the filter actually narrows roles — this fixes the WLNCentral ROLE-001 bug (detached-copy override silently dropped M2M role-assignment writes); (2) the popup auto-shows once via `Frame.ViewChanged` on the main window, shown platform-agnostically with `PopupWindowShowAction.GetPopupWindowParams()` + `ShowViewParameters(TargetWindow.NewModalWindow)` (mirrors DevExpress's own Blazor binding, verified in source); (3) the fragile `CloseAllTabs` reflection machinery is deleted — no tabs exist yet at selection time.

**Tech Stack:** .NET 8, DevExpress XAF v25.2.3, EF Core, Playwright (.NET/NUnit).

## Global Constraints

- `src/RoleChooser` stays platform-agnostic: references only `DevExpress.ExpressApp` + `DevExpress.ExpressApp.Security` — no Blazor/Win assemblies.
- `PermissionsReloadMode.NoCache` **remains required**. Verified in DevExpress source: `SecurityStrategy.ReloadPermissionsCore()` (SecurityStrategy.cs:319) only reloads the User object; it does not invalidate a permission cache. NoCache is what makes post-selection ObjectSpaces re-evaluate against the filtered roles.
- All `ILogger<T>` dependencies stay optional (`ILogger<T>? logger = null`) — required loggers crash consuming apps at DI validation.
- Runtime verification workflow: implementer **builds only**; Martin runs the demo from Visual Studio. Playwright tests run against `https://localhost:5001` with Docker SQL up (`docker compose up -d`).
- DevExpress behavior must be verified against dxdocs or the installed source at `C:\Program Files\DevExpress 25.2\Components\Sources\DevExpress.ExpressApp` — never assumed. APIs already verified for this plan: `PopupWindowShowAction.GetPopupWindowParams()` is public and wires `DialogController.Accepting → Execute` (PopupWindowShowAction.cs:186-210); `Frame.ViewChanged` is public (Frame.cs:583); the Blazor popup binding builds `ShowViewParameters` with `TargetWindow.NewModalWindow`, `CreateAllControllers = true`, and adds `args.DialogController.Controllers` then `args.DialogController` (PopupWindowShowActionBinding.cs:64-73).

---

### Task 1: Pass-through filter (ROLE-001 fix)

**Files:**
- Modify: `src/RoleChooser/Services/IActiveRoleFilter.cs`
- Modify: `src/RoleChooser/Services/ActiveRoleFilter.cs`
- Modify: `src/RoleChooser/Security/RoleChooserUserBase.cs:15-37`

**Interfaces:**
- Consumes: existing `IActiveRoleFilter` members.
- Produces: `bool IActiveRoleFilter.IsFiltering { get; }` (true only when the active set is a strict subset of available roles) and `bool IActiveRoleFilter.SelectionMade { get; }` (true once `SetActiveRoles` has run this session). Task 2 gates the interstitial on `SelectionMade == false` and `AvailableRoles.Count >= 2`.

- [ ] **Step 1: Add `IsFiltering` and `SelectionMade` to the interface**

Append inside `IActiveRoleFilter` (after `IsRoleActive`):

```csharp
    /// <summary>
    /// True only when the user narrowed their roles (active set is a strict subset
    /// of available roles). When false, the Roles override must return the real
    /// tracked collection so M2M role assignment writes persist.
    /// </summary>
    bool IsFiltering { get; }

    /// <summary>
    /// True once the user has made (or skipped past) their session role selection.
    /// </summary>
    bool SelectionMade { get; }
```

- [ ] **Step 2: Implement in `ActiveRoleFilter`**

Add the properties and set `SelectionMade` in the existing methods:

```csharp
    public bool IsFiltering => _activeRoleIds.Count < _availableRoles.Count;
    public bool SelectionMade { get; private set; }
```

In `Initialize(...)`, add as the first line of the method body:

```csharp
        SelectionMade = false;
```

In `SetActiveRoles(...)`, add after `_activeRoleIds = new HashSet<Guid>(roleIds);`:

```csharp
        SelectionMade = true;
```

- [ ] **Step 3: Make the `Roles` override pass-through**

In `RoleChooserUserBase.Roles` getter, change the guard (currently `if (filter == null || allRoles is not { Count: > 0 })`) to:

```csharp
            if (filter == null || !filter.IsFiltering || allRoles is not { Count: > 0 })
            {
                _logger?.LogDebug("Roles getter — pass-through, returning {Count} unfiltered roles",
                    allRoles?.Count ?? 0);
                return allRoles;
            }
```

This is the ROLE-001 fix: whenever the user selected everything (or the chooser was skipped), XAF gets `base.Roles` — the live EF change-tracking collection — so Link/Unlink writes persist. Only a deliberately narrowed session still sees a filtered (read-only-in-practice) copy, which is acceptable: a narrowed session is intentionally less privileged.

- [ ] **Step 4: Build**

Run: `dotnet build XafRoleChooser.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/RoleChooser/Services/IActiveRoleFilter.cs src/RoleChooser/Services/ActiveRoleFilter.cs src/RoleChooser/Security/RoleChooserUserBase.cs
git commit -m "feat(rolechooser): pass-through Roles override when not filtering (fixes silent M2M role-write loss)"
```

---

### Task 2: Login-time interstitial, remove switch machinery

**Files:**
- Modify: `src/RoleChooser/Controllers/RoleChooserWindowController.cs` (full rewrite below)
- Modify: `src/RoleChooser/RoleChooserModule.cs:50-56` (warning wording only)

**Interfaces:**
- Consumes: `IActiveRoleFilter.SelectionMade`, `IActiveRoleFilter.IsFiltering` (Task 1); `PopupWindowShowAction.GetPopupWindowParams()`; `Frame.ViewChanged`.
- Produces: popup auto-shows once per main window when `!SelectionMade && AvailableRoles.Count >= 2`; the Tools "Active Roles" action deactivates permanently once a selection is made (login-time-only semantics). `CloseAllTabs` is deleted.

- [ ] **Step 1: Rewrite the controller**

Replace the entire body of `RoleChooserWindowController.cs` with:

```csharp
using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.SystemModule;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoleChooser.BusinessObjects;
using RoleChooser.Services;

namespace RoleChooser.Controllers;

/// <summary>
/// Shows the role-selection popup once, right after login (before the user
/// starts working), when the user has two or more optional roles. Selection
/// is per-session; changing roles afterwards requires re-login.
/// </summary>
public class RoleChooserWindowController : WindowController
{
    private const string LoginTimeOnlyKey = "RoleChooser.LoginTimeOnly";

    private PopupWindowShowAction _chooseRolesAction;
    private IActiveRoleFilter? _roleFilter;
    private ILogger<RoleChooserWindowController>? _logger;
    private bool _popupShown;

    public RoleChooserWindowController()
    {
        TargetWindowType = WindowType.Main;

        _chooseRolesAction = new PopupWindowShowAction(this, "ChooseActiveRoles", DevExpress.Persistent.Base.PredefinedCategory.Tools.ToString())
        {
            Caption = "Active Roles",
            ImageName = "Security_Role",
            ToolTip = "Choose which roles are active for this session"
        };
        _chooseRolesAction.CustomizePopupWindowParams += ChooseRolesAction_CustomizePopupWindowParams;
        _chooseRolesAction.Execute += ChooseRolesAction_Execute;
    }

    protected override void OnActivated()
    {
        base.OnActivated();
        _roleFilter = Application.ServiceProvider.GetService<IActiveRoleFilter>();
        _logger = Application.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<RoleChooserWindowController>();
        UpdateActionActive();
        Window.ViewChanged += Window_ViewChanged;
    }

    protected override void OnDeactivated()
    {
        Window.ViewChanged -= Window_ViewChanged;
        _roleFilter = null;
        _logger = null;
        base.OnDeactivated();
    }

    private void UpdateActionActive()
    {
        _chooseRolesAction.Active.SetItemValue(LoginTimeOnlyKey,
            _roleFilter is { SelectionMade: false } && _roleFilter.AvailableRoles.Count >= 2);
    }

    private void Window_ViewChanged(object? sender, ViewChangedEventArgs e)
    {
        if (_popupShown || Window.View == null) return;
        if (_roleFilter is not { SelectionMade: false } || _roleFilter.AvailableRoles.Count < 2)
        {
            _logger?.LogInformation("Login-time chooser skipped — SelectionMade: {SelectionMade}, optional roles: {Count}",
                _roleFilter?.SelectionMade, _roleFilter?.AvailableRoles.Count ?? 0);
            _popupShown = true;
            return;
        }

        _popupShown = true;
        _logger?.LogInformation("Showing login-time role chooser — {Count} optional roles", _roleFilter.AvailableRoles.Count);
        ShowChooserPopup();
    }

    /// <summary>
    /// Shows the chooser popup platform-agnostically. Mirrors the generic core of
    /// DevExpress's Blazor PopupWindowShowActionBinding.ShowPopupWindow (verified
    /// against installed source, v25.2): GetPopupWindowParams() wires the
    /// DialogController's Accepting event to this action's Execute handler.
    /// </summary>
    private void ShowChooserPopup()
    {
        var args = _chooseRolesAction.GetPopupWindowParams();
        if (args.View == null) return;

        // Cancel = "keep all roles" — counts as the session selection.
        args.DialogController.Cancelling += (s, e) => ConfirmAllRoles();

        var svp = new ShowViewParameters(args.View)
        {
            TargetWindow = TargetWindow.NewModalWindow,
            CreateAllControllers = true
        };
        svp.Controllers.AddRange(args.DialogController.Controllers);
        svp.Controllers.Add(args.DialogController);
        Application.ShowViewStrategy.ShowView(svp, new ShowViewSource(Frame, _chooseRolesAction));
    }

    private void ConfirmAllRoles()
    {
        if (_roleFilter == null) return;
        _roleFilter.SetActiveRoles(_roleFilter.AvailableRoles.Select(r => r.Id));
        UpdateActionActive();
        _logger?.LogInformation("Chooser cancelled — all {Count} roles confirmed active", _roleFilter.AvailableRoles.Count);
    }

    private void ChooseRolesAction_CustomizePopupWindowParams(object sender, CustomizePopupWindowParamsEventArgs e)
    {
        if (_roleFilter == null) return;

        var os = Application.CreateObjectSpace(typeof(ActiveRoleSelection));
        var items = new BindingList<ActiveRoleSelection>();

        foreach (var (id, name) in _roleFilter.AvailableRoles)
        {
            var item = os.CreateObject<ActiveRoleSelection>();
            item.RoleId = id;
            item.RoleName = name;
            item.IsActive = _roleFilter.ActiveRoleIds.Contains(id);
            items.Add(item);
        }

        // NonPersistentObjectSpace needs ObjectsGetting to provide objects for ListView
        if (os is NonPersistentObjectSpace npOs)
        {
            npOs.ObjectsGetting += (s, argsGetting) =>
            {
                if (argsGetting.ObjectType == typeof(ActiveRoleSelection))
                {
                    argsGetting.Objects = items;
                }
            };
        }

        var listView = Application.CreateListView(os, typeof(ActiveRoleSelection), true);
        e.View = listView;
        e.DialogController.SaveOnAccept = false;
    }

    private void ChooseRolesAction_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
    {
        if (_roleFilter == null) return;

        var selectedItems = e.PopupWindowViewSelectedObjects.Cast<ActiveRoleSelection>().ToList();
        var selectedRoleIds = selectedItems.Select(i => i.RoleId).ToList();

        _logger?.LogInformation("Session roles selected — Active: [{ActiveRoles}] | Always-active: {AlwaysActive}",
            string.Join(", ", selectedItems.Select(i => i.RoleName)),
            _roleFilter.AlwaysActiveRoleName ?? "(none)");

        // Capture refs before any view churn — closing/replacing views can deactivate this controller.
        var app = Application;
        var navController = Frame?.GetController<ShowNavigationItemController>();

        _roleFilter.SetActiveRoles(selectedRoleIds);
        UpdateActionActive();

        // No CloseAllTabs: at login time no tabs exist yet.
        if (app?.Security is ISecurityStrategyBase securityStrategy)
        {
            securityStrategy.ReloadPermissions();
        }

        if (navController != null)
        {
            navController.RecreateNavigationItems();
            var startupItem = navController.GetStartupNavigationItem();
            if (startupItem != null)
            {
                navController.ShowNavigationItemAction.DoExecute(startupItem);
            }
        }
    }
}
```

Notes for the implementer:
- The `using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;` and `using RoleChooser.Security;` directives from the old file are intentionally gone (nothing references them after `CloseAllTabs` is deleted). If the build says otherwise, keep what it needs.
- `ShowViewSource(Frame, _chooseRolesAction)` — if the constructor signature differs, check `ShowViewSource` in the DevExpress source (`DevExpress.ExpressApp\ShowViewStrategyBase.cs` area) and adapt.
- Known risk (runtime-only): showing a modal synchronously inside `ViewChanged` may misbehave on Blazor's first render. If the popup doesn't appear for MultiRole login, the fallback is to defer one render cycle — verify against the DevExpress source how `BlazorApplication` schedules `ShowView`, do not guess.

- [ ] **Step 2: Update the module's NoCache warning wording**

In `RoleChooserModule.Application_SetupComplete`, replace the warning message string (keep the structure):

```csharp
                logger?.LogWarning(
                    "RoleChooserModule requires PermissionsReloadMode.NoCache — without it the " +
                    "login-time role selection does not take effect (permissions stay cached from logon). " +
                    "Current mode is {Mode}.",
                    strategy.PermissionsReloadMode);
```

- [ ] **Step 3: Build**

Run: `dotnet build XafRoleChooser.slnx`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Hand off for VS runtime check (do not start the app yourself)**

Ask Martin to run the Blazor demo from Visual Studio and confirm: MultiRole login → popup appears before working; User login → no popup; cancel → everything stays visible. WinForms same checks.

- [ ] **Step 5: Commit**

```bash
git add src/RoleChooser/Controllers/RoleChooserWindowController.cs src/RoleChooser/RoleChooserModule.cs
git commit -m "feat(rolechooser): login-time role selection interstitial, drop mid-session switch machinery"
```

---

### Task 3: Demo seed user + Playwright tests

**Files:**
- Modify: `XafRoleChooser/XafRoleChooser.Module/DatabaseUpdate/Updater.cs` (add `SingleRole` user)
- Modify: `tests/XafRoleChooser.Playwright/Tests/RoleSwitchingTests.cs` (rewrite for interstitial flow)
- Modify: `tests/XafRoleChooser.Playwright/Tests/RoleChooserTests.cs` (popup-appearance tests)
- Modify: `tests/XafRoleChooser.Playwright/PageObjects/MainPage.cs` (only if a new helper is needed)

**Interfaces:**
- Consumes: the interstitial behavior from Task 2; existing page objects — `LoginPage` (login selector `input.dxbl-text-edit-input[type='text']`, button `button.xaf-action[data-action-name='Log In']:not([dxbl-virtual-el])`), `MainPage` popup helpers (popup located via `GetByText("Active Role Selection", new() { Exact = true })`, row selection scoped to `dxbl-popup-root`, nav via `.xaf-accordion >> text='...'`).
- Produces: green E2E suite covering: auto-popup, skip rule, narrowing, cancel, and the ROLE-001 regression (role Link/Unlink persists).

- [ ] **Step 1: Add the `SingleRole` seed user**

In `Updater.cs`, after the `MultiRole` block (follows the existing pattern exactly):

```csharp
            if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, "SingleRole") == null)
            {
                string EmptyPassword = "";
                _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "SingleRole", EmptyPassword, (user) =>
                {
                    user.Roles.Add(defaultRole);
                    user.Roles.Add(salesRole);
                });
            }
```

- [ ] **Step 2: Rewrite the E2E tests for the new flow**

Test matrix (adapt to the existing page-object style; each test = fresh login):

| Test | User | Expected |
|---|---|---|
| `Login_MultiRole_ShowsChooserAutomatically` | MultiRole | "Active Role Selection" popup visible without clicking anything |
| `Login_DefaultOnlyUser_SkipsChooser` | User | No popup; app usable immediately |
| `Login_SingleOptionalRole_SkipsChooser` | SingleRole | No popup; Sales nav group visible (both roles active) |
| `Chooser_SelectSubset_NavigationReflectsSelection` | MultiRole | Select only HR Manager row → Accept → HR nav group visible, Finance absent |
| `Chooser_Cancel_KeepsAllRoles` | MultiRole | Cancel popup → all nav groups visible |
| `Chooser_AfterSelection_ToolsActionGone` | MultiRole | Accept popup → Tools tab has no "Active Roles" button |
| `RoleAssignment_LinkUnlink_Persists` (ROLE-001 regression) | Admin, accept all roles | Open Users → User detail → Roles tab → Link Sales → save → log out → log back in as Admin → Sales still linked; then Unlink to restore seed state |

Concrete skeleton for the regression test (the load-bearing one):

```csharp
    [Test]
    public async Task RoleAssignment_LinkUnlink_Persists()
    {
        await LoginPage.LoginAsync("Admin");
        await MainPage.AcceptRoleChooserSelectingAllAsync();   // popup: select all rows, Accept

        await MainPage.NavigateAsync("Users", "Application User");
        await MainPage.OpenListViewRowAsync("User");
        await MainPage.OpenTabAsync("Roles");
        await MainPage.LinkObjectAsync("Sales");               // M2M Link action
        await MainPage.SaveAndCloseAsync();

        await MainPage.LogoutAsync();
        await LoginPage.LoginAsync("Admin");
        await MainPage.AcceptRoleChooserSelectingAllAsync();

        await MainPage.NavigateAsync("Users", "Application User");
        await MainPage.OpenListViewRowAsync("User");
        await MainPage.OpenTabAsync("Roles");
        await Assertions.Expect(MainPage.RolesGridRow("Sales")).ToBeVisibleAsync();  // FAILS on old code

        await MainPage.UnlinkObjectAsync("Sales");             // restore seed state
        await MainPage.SaveAndCloseAsync();
    }
```

Helpers that don't exist yet (`AcceptRoleChooserSelectingAllAsync`, `LinkObjectAsync`, `UnlinkObjectAsync`, `RolesGridRow`, `OpenTabAsync`, `OpenListViewRowAsync`, `LogoutAsync`) go in `MainPage.cs`, built from the documented selector patterns above (scope all popup interaction to `dxbl-popup-root`; XAF action buttons use Caption in `data-action-name`).

- [ ] **Step 3: Run the suite**

Run: `docker compose up -d`, then Martin starts the app from VS (or CI-style: `dotnet run --project XafRoleChooser/XafRoleChooser.Blazor.Server/XafRoleChooser.Blazor.Server.csproj` in background — kill it afterwards), then `dotnet test tests/XafRoleChooser.Playwright/`
Expected: all tests green. `RoleAssignment_LinkUnlink_Persists` is the proof that ROLE-001 is dead.

- [ ] **Step 4: Commit**

```bash
git add XafRoleChooser/XafRoleChooser.Module/DatabaseUpdate/Updater.cs tests/XafRoleChooser.Playwright/
git commit -m "test: E2E coverage for login-time role selection and role-assignment persistence"
```

---

### Task 4: Documentation

**Files:**
- Modify: `docs/how-to-implement.md`
- Modify: `README.md`
- Modify: `CLAUDE.md` (Key flow paragraph in "RoleChooser Module Architecture")

**Interfaces:** none (prose).

- [ ] **Step 1: Update `docs/how-to-implement.md`**

- Key characteristics: replace "permissions update live when the user changes their active roles" with "roles are chosen once, right after login; changing them requires re-login. The chooser is skipped when the user has fewer than two optional roles."
- "How it works" section: replace steps 4-7 with the interstitial flow (auto-popup on first main-window view, Accept → `SetActiveRoles` + `ReloadPermissions` + `RecreateNavigationItems`, Cancel → all roles active).
- Keep the `PermissionsReloadMode.NoCache` prerequisite (Step 5) — still required; update its rationale sentence to reference login-time selection.
- Add to the integration checklist: every user **must** hold the always-active role ("Default") — the module does not validate this and a user without it can end up with no access.
- Troubleshooting: replace "Roles revert after navigation" with "Chooser doesn't appear" (check ≥2 optional roles, filter registered in LoggedOn) and add "Role assignment from User detail view doesn't save" → only possible in a *narrowed* session; administer roles in a session where all roles were selected.

- [ ] **Step 2: Update `README.md` and `CLAUDE.md`**

README: feature bullet + flow diagram caption ("switch" → "choose at login"). The Excalidraw source `docs/rolechooser-flow.excalidraw` gets a follow-up session if the diagram needs redrawing — note it in TODO.md, don't block on it.
CLAUDE.md Key flow paragraph, replace with: Login → `LoggedOn` initializes filter (all roles active) → main window shows → chooser popup auto-appears (skipped if <2 optional roles) → Accept: selected rows = active roles → `SetActiveRoles()` + `ReloadPermissions()` → recreate navigation → startup view. No mid-session switching; `Roles` override is pass-through unless the session was narrowed.

- [ ] **Step 3: Commit**

```bash
git add docs/how-to-implement.md README.md CLAUDE.md
git commit -m "docs: describe login-time role selection flow"
```

---

## Self-Review Notes

- Spec coverage: interstitial ✔ (Task 2), skip-if-<2-optional-roles ✔ (Task 2 + Task 3 seed user), Default hidden-and-always-applied ✔ (unchanged — `AlwaysActiveRoleName` excluded from `AvailableRoles` at load), ROLE-001 pass-through fix ✔ (Task 1 + regression test Task 3), machinery removal ✔ (Task 2 deletes `CloseAllTabs`).
- Type consistency: `IsFiltering`/`SelectionMade` defined in Task 1, consumed by name in Task 2's `UpdateActionActive`/`Window_ViewChanged`. `ActiveRoleSelection.RoleId/RoleName/IsActive` unchanged.
- Deliberate ceiling: a narrowed session still gets the detached-copy filtered list (`// narrowed session = read-only roles by design`); upgrade path is a live filtered wrapper IList if anyone ever needs to administer roles from a narrowed session.
