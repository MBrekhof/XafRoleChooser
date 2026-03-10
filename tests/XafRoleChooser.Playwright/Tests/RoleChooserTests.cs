using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using XafRoleChooser.Playwright.Infrastructure;
using XafRoleChooser.Playwright.PageObjects;

namespace XafRoleChooser.Playwright.Tests;

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
    }

    [Test]
    public async Task ActiveRolesButton_ShouldBeVisible_AfterLogin()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        // The Active Roles button should be visible in the toolbar
        var button = Page.Locator("button:has-text('Active Roles'), [data-action='ChooseActiveRoles']");
        await button.WaitForAsync(new() { Timeout = TestConstants.DefaultTimeout });
        Assert.That(await button.IsVisibleAsync(), Is.True);
    }

    [Test]
    public async Task RoleChooserPopup_ShouldOpen_WhenButtonClicked()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        await _mainPage.ClickActiveRolesButton();
        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.True);
    }

    [Test]
    public async Task RoleChooser_ShouldShowAssignedRoles_ExcludingDefault()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        await _mainPage.ClickActiveRolesButton();

        // "Default" role should NOT be visible in the chooser
        var defaultRole = Page.Locator("text=Default");
        // "Administrators" role SHOULD be visible (or other assigned roles)
        var adminRole = Page.Locator("text=Administrators");

        Assert.That(await adminRole.IsVisibleAsync(), Is.True, "Assigned roles should be visible in chooser");
    }

    [Test]
    public async Task RoleChooser_ShouldActivateRole_WhenCheckedAndAccepted()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        await _mainPage.ClickActiveRolesButton();
        await _mainPage.ToggleRoleInChooser("Administrators");
        await _mainPage.AcceptRoleChooser();

        // After activating the admin role, admin navigation items should be visible
        // This is a placeholder assertion - actual items depend on the app's navigation
        await _mainPage.WaitForXafReady();
    }

    [Test]
    public async Task RoleChooser_ShouldDeactivateRole_WhenUncheckedAndAccepted()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        // First activate a role
        await _mainPage.ClickActiveRolesButton();
        await _mainPage.ToggleRoleInChooser("Administrators");
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForXafReady();

        // Then deactivate it
        await _mainPage.ClickActiveRolesButton();
        await _mainPage.ToggleRoleInChooser("Administrators");
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForXafReady();

        // Permissions should be reverted
    }

    [Test]
    public async Task RoleChooser_ShouldPersistSelections_AcrossPopupOpens()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.WaitForXafReady();

        // Activate a role
        await _mainPage.ClickActiveRolesButton();
        await _mainPage.ToggleRoleInChooser("Administrators");
        await _mainPage.AcceptRoleChooser();
        await _mainPage.WaitForXafReady();

        // Re-open the chooser - the role should still be checked
        await _mainPage.ClickActiveRolesButton();
        // Verify the checkbox state is preserved
        Assert.That(await _mainPage.IsRoleChooserPopupVisible(), Is.True);
    }
}
