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

    private async Task DeactivateRoles(params string[] roleNames)
    {
        await _mainPage.ClickActiveRolesButton();
        foreach (var role in roleNames)
        {
            await _mainPage.ToggleRoleInChooser(role);
        }
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForNavigationRefresh();
    }

    [Test]
    public async Task RoleSwitch_HRManagerOnly_ShowsHRNavigation()
    {
        // Arrange: Login as Admin (all roles active by default)
        await LoginAsAdmin();

        // Act: Deactivate all roles except HR Manager
        await DeactivateRoles(
            TestConstants.RoleAdministrators,
            TestConstants.RoleProjectManager,
            TestConstants.RoleSales,
            TestConstants.RoleFinance
        );

        // Assert: HR and Company visible, others hidden
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
        // Arrange: Login as Admin (all roles active by default)
        await LoginAsAdmin();

        // Act: Deactivate all roles except Sales
        await DeactivateRoles(
            TestConstants.RoleAdministrators,
            TestConstants.RoleHRManager,
            TestConstants.RoleProjectManager,
            TestConstants.RoleFinance
        );

        // Assert: Sales, Company, Projects visible; HR and Finance hidden
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
        // Arrange: Login as Admin (all roles active by default)
        await LoginAsAdmin();

        // Act: Deactivate all roles except Finance
        await DeactivateRoles(
            TestConstants.RoleAdministrators,
            TestConstants.RoleHRManager,
            TestConstants.RoleProjectManager,
            TestConstants.RoleSales
        );

        // Assert: Finance, Sales, Company visible; HR and Projects hidden
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
        // Arrange: Login as Admin (all roles active by default)
        await LoginAsAdmin();

        // Act Step 1: Deactivate ALL roles
        await DeactivateRoles(
            TestConstants.RoleAdministrators,
            TestConstants.RoleHRManager,
            TestConstants.RoleProjectManager,
            TestConstants.RoleSales,
            TestConstants.RoleFinance
        );

        // Act Step 2: Reopen popup and reactivate only Administrators
        await DeactivateRoles(TestConstants.RoleAdministrators); // toggles it back on

        // Assert: All navigation groups should be visible (Administrators has full access)
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavCompany), Is.True,
            "Company navigation should be visible with Administrators role");
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
        // Arrange: Login as Admin (all roles active by default)
        await LoginAsAdmin();

        // Act: Deactivate ALL roles (only Default remains, which has no nav permissions)
        await DeactivateRoles(
            TestConstants.RoleAdministrators,
            TestConstants.RoleHRManager,
            TestConstants.RoleProjectManager,
            TestConstants.RoleSales,
            TestConstants.RoleFinance
        );

        // Assert: All navigation groups should be hidden
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
