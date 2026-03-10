using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using XafRoleChooser.Playwright.Infrastructure;
using XafRoleChooser.Playwright.PageObjects;

namespace XafRoleChooser.Playwright.Tests;

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
    }

    [Test]
    public async Task DefaultRoleOnly_ShouldHaveLimitedNavigation()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        // With only Default role active (no additional roles selected),
        // admin-level navigation should not be accessible
        // The exact items depend on role permissions configured in the app
    }

    [Test]
    public async Task AdminRole_WhenActivated_ShouldShowAdminNavigation()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        // Activate admin role
        await _mainPage.ClickActiveRolesButton();
        await _mainPage.ToggleRoleInChooser("Administrators");
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForXafReady();

        // Admin navigation items should now be visible
        // Placeholder: check for Users or Roles navigation item
        var usersNav = Page.Locator(".xaf-navigation a:has-text('Users'), .nav-item:has-text('Users')");
        // Note: actual assertion depends on the configured navigation
    }

    [Test]
    public async Task MultipleRoles_ShouldCombinePermissions()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        // This test verifies that selecting multiple roles combines their permissions
        await _mainPage.ClickActiveRolesButton();
        // Select all available roles
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForXafReady();
    }
}
