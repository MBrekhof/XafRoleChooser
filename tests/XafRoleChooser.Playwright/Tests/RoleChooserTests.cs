using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using XafRoleChooser.Playwright.Infrastructure;
using XafRoleChooser.Playwright.PageObjects;

namespace XafRoleChooser.Playwright.Tests;

/// <summary>
/// Covers the login-time interstitial: auto-appearance, the skip rule for users
/// with fewer than two optional roles, narrowing via row selection, cancel, and
/// the Tools ribbon action going away once a selection has been made.
/// Rewritten for the login-time flow — the old mid-session "click Tools > Active
/// Roles to switch" behavior no longer exists.
/// </summary>
[TestFixture]
public class RoleChooserTests : PageTest
{
    private LoginPage _loginPage = null!;
    private MainPage _mainPage = null!;

    [SetUp]
    public async Task SetUp()
    {
        _loginPage = new LoginPage(Page);
        _mainPage = new MainPage(Page);
        await _loginPage.NavigateTo();
    }

    [Test]
    public async Task Login_MultiRole_ShowsChooserAutomatically()
    {
        await _loginPage.Login(TestConstants.MultiRoleUser);

        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.True,
            "Active Role Selection popup should appear automatically for a user with >=2 optional roles");
    }

    [Test]
    public async Task Login_DefaultOnlyUser_SkipsChooser()
    {
        await _loginPage.Login(TestConstants.RegularUser);
        await _mainPage.WaitForXafReady();

        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.False,
            "Chooser should not appear for a user with 0 optional roles");
        Assert.That(await _mainPage.IsLoggedIn(), Is.True, "App should be usable immediately");
    }

    [Test]
    public async Task Login_SingleOptionalRole_SkipsChooser()
    {
        await _loginPage.Login(TestConstants.SingleRoleUser);
        await _mainPage.WaitForXafReady();

        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.False,
            "Chooser should not appear for a user with exactly 1 optional role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True,
            "Sales navigation should be visible — the single optional role stays active without narrowing");
    }

    [Test]
    public async Task Chooser_SelectSubset_NavigationReflectsSelection()
    {
        await _loginPage.Login(TestConstants.MultiRoleUser);
        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.True);

        await _mainPage.SelectRoleInChooser(TestConstants.RoleHRManager);
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForNavigationRefresh();

        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True,
            "HR navigation should be visible with HR Manager selected");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
            "Finance navigation should be hidden when not selected");
    }

    [Test]
    public async Task Chooser_Cancel_KeepsAllRoles()
    {
        await _loginPage.Login(TestConstants.MultiRoleUser);
        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.True);

        await _mainPage.CancelRoleChooserAsync();
        await _mainPage.WaitForNavigationRefresh();

        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True);
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True);
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavFinance), Is.True);
    }

    [Test]
    public async Task Chooser_AfterSelection_ToolsActionGone()
    {
        await _loginPage.Login(TestConstants.MultiRoleUser);
        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.True);

        await _mainPage.AcceptRoleChooserSelectingAllAsync();
        await _mainPage.WaitForNavigationRefresh();

        // Deactivating the only Tools action removes the whole Tools ribbon tab.
        // Clicking Tools when it's visible makes the check stronger (exercises the
        // tab), but the decisive assertion below must hold regardless of which
        // branch fires — the else branch used to have no assertion at all.
        var toolsTab = Page.GetByText("Tools", new() { Exact = true }).First;
        if (await toolsTab.IsVisibleAsync())
        {
            await toolsTab.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        Assert.That(await Page.Locator("button.xaf-action[data-action-name='Active Roles']:not([dxbl-virtual-el])").CountAsync(), Is.EqualTo(0),
            "Active Roles action should be deactivated after the session selection");
    }
}
