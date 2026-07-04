using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using XafRoleChooser.Playwright.Infrastructure;
using XafRoleChooser.Playwright.PageObjects;

namespace XafRoleChooser.Playwright.Tests;

/// <summary>
/// Permission-combination checks, adapted to the login-time interstitial flow
/// (the mid-session Tools > Active Roles switch no longer exists).
/// </summary>
[TestFixture]
public class PermissionTests : PageTest
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
    public async Task DefaultRoleOnly_ShouldHaveLimitedNavigation()
    {
        await _loginPage.Login(TestConstants.RegularUser);
        await _mainPage.WaitForXafReady();

        Assert.That(await _mainPage.IsLoggedIn(), Is.True);
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavHR), Is.True,
            "HR navigation should be hidden with only the Default role");
        Assert.That(await _mainPage.IsNavGroupHidden(TestConstants.NavFinance), Is.True,
            "Finance navigation should be hidden with only the Default role");
    }

    [Test]
    public async Task AdminRole_WhenActivated_ShouldShowAdminNavigation()
    {
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForRoleChooserPopupAsync();

        // Select only Administrators in the login-time chooser
        await _mainPage.SelectRoleInChooser(TestConstants.RoleAdministrators);
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForNavigationRefresh();

        // Administrators (IsAdministrative) grants access to all business nav groups
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True,
            "HR navigation should be visible with the Administrators role");
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavSales), Is.True,
            "Sales navigation should be visible with the Administrators role");
    }

    [Test]
    public async Task MultipleRoles_ShouldCombinePermissions()
    {
        await _loginPage.Login(TestConstants.AdminUser);

        // Select all roles in the login-time chooser — permissions combine
        await _mainPage.AcceptRoleChooserSelectingAllAsync();
        await _mainPage.WaitForNavigationRefresh();

        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavHR), Is.True);
        Assert.That(await _mainPage.IsNavGroupVisible(TestConstants.NavFinance), Is.True);
    }
}
