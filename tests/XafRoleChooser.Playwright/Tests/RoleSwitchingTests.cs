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

    private async Task LoginAsAdmin()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();
    }

    /// <summary>
    /// Opens the role chooser popup, selects only the specified roles via row selection checkboxes, and accepts.
    /// Roles NOT in the list will be unselected (deactivated).
    /// </summary>
    private async Task SetActiveRoles(params string[] roleNames)
    {
        await _mainPage.ClickActiveRolesButton();
        foreach (var role in roleNames)
        {
            await _mainPage.SelectRoleInChooser(role);
        }
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForNavigationRefresh();
    }

    [Test]
    public async Task RoleSwitch_HRManagerOnly_ShowsHRNavigation()
    {
        await LoginAsAdmin();

        // Select only HR Manager role
        await SetActiveRoles(TestConstants.RoleHRManager);

        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True,
            "HR navigation should be visible with HR Manager role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True,
            "Company navigation should be visible with HR Manager role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavProjects), Is.True,
            "Projects navigation should be hidden without Project Manager role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavSales), Is.True,
            "Sales navigation should be hidden without Sales role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
            "Finance navigation should be hidden without Finance role");
    }

    [Test]
    public async Task RoleSwitch_SalesOnly_ShowsSalesNavigation()
    {
        await LoginAsAdmin();

        // Select only Sales role
        await SetActiveRoles(TestConstants.RoleSales);

        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True,
            "Sales navigation should be visible with Sales role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True,
            "Company navigation should be visible with Sales role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavProjects), Is.True,
            "Projects navigation should be visible with Sales role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavHR), Is.True,
            "HR navigation should be hidden without HR Manager role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
            "Finance navigation should be hidden without Finance role");
    }

    [Test]
    public async Task RoleSwitch_FinanceOnly_ShowsFinanceNavigation()
    {
        await LoginAsAdmin();

        // Select only Finance role
        await SetActiveRoles(TestConstants.RoleFinance);

        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavFinance), Is.True,
            "Finance navigation should be visible with Finance role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True,
            "Sales navigation should be visible with Finance role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True,
            "Company navigation should be visible with Finance role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavHR), Is.True,
            "HR navigation should be hidden without HR Manager role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavProjects), Is.True,
            "Projects navigation should be hidden without Project Manager role");
    }

    [Test]
    public async Task RoleSwitch_ReactivateAdministrators_ShowsAllNavigation()
    {
        await LoginAsAdmin();

        // Step 1: Deactivate all roles (select none)
        await SetActiveRoles();

        // Step 2: Reactivate only Administrators
        await SetActiveRoles(TestConstants.RoleAdministrators);

        // Administrators (IsAdministrative) gives access to all nav groups
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True,
            "HR navigation should be visible with Administrators role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavProjects), Is.True,
            "Projects navigation should be visible with Administrators role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True,
            "Sales navigation should be visible with Administrators role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavFinance), Is.True,
            "Finance navigation should be visible with Administrators role");
    }

    [Test]
    public async Task RoleSwitch_DeactivateAll_HidesAllNavigation()
    {
        await LoginAsAdmin();

        // Select no roles (just open popup and accept)
        await SetActiveRoles();

        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavCompany), Is.True,
            "Company navigation should be hidden with no active roles");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavHR), Is.True,
            "HR navigation should be hidden with no active roles");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavProjects), Is.True,
            "Projects navigation should be hidden with no active roles");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavSales), Is.True,
            "Sales navigation should be hidden with no active roles");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
            "Finance navigation should be hidden with no active roles");
    }
}
