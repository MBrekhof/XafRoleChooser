using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using XafRoleChooser.Playwright.Infrastructure;
using XafRoleChooser.Playwright.PageObjects;

namespace XafRoleChooser.Playwright.Tests;

/// <summary>
/// ROLE-001 regression coverage: after the Roles override became login-time-only
/// (pass-through when the user hasn't narrowed roles), M2M role Link/Unlink from
/// the User DetailView must still persist. Previously the override could silently
/// swallow role writes made through the standard security UI.
/// The old mid-session "switch active roles then assert nav changes" tests that
/// used to live in this file were removed — that flow no longer exists after the
/// login-time interstitial rewrite; equivalent coverage now lives in
/// RoleChooserTests (Chooser_SelectSubset_NavigationReflectsSelection etc).
/// </summary>
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
    public async Task RoleAssignment_LinkUnlink_Persists()
    {
        await _loginPage.NavigateTo();
        await _loginPage.Login(TestConstants.AdminUser);
        await _mainPage.AcceptRoleChooserSelectingAllAsync(); // popup: select all rows, Accept

        await _mainPage.NavigateAsync(TestConstants.NavDefault, "Users");
        await _mainPage.OpenListViewRowAsync(TestConstants.RegularUser);

        // Idempotent pre-check: a previous run that crashed between Link and Unlink
        // can leave Sales linked to the User account (dirty seed state).
        // LinkObjectAsync assumes the row isn't already linked, so clear it first.
        if (await _mainPage.RolesGridRow(TestConstants.RoleSales).IsVisibleAsync())
        {
            await _mainPage.UnlinkObjectAsync(TestConstants.RoleSales);
            await _mainPage.SaveAndCloseAsync();
            await _mainPage.NavigateAsync(TestConstants.NavDefault, "Users");
            await _mainPage.OpenListViewRowAsync(TestConstants.RegularUser);
        }

        var linked = false;
        try
        {
            await _mainPage.LinkObjectAsync(TestConstants.RoleSales);
            linked = true;
            await _mainPage.SaveAndCloseAsync();

            await _mainPage.Logout();
            await _loginPage.NavigateTo();
            await _loginPage.Login(TestConstants.AdminUser);
            await _mainPage.AcceptRoleChooserSelectingAllAsync();

            await _mainPage.NavigateAsync(TestConstants.NavDefault, "Users");
            await _mainPage.OpenListViewRowAsync(TestConstants.RegularUser);
            await Assertions.Expect(_mainPage.RolesGridRow(TestConstants.RoleSales)).ToBeVisibleAsync();

            // Restore seed state
            await _mainPage.UnlinkObjectAsync(TestConstants.RoleSales);
            await _mainPage.SaveAndCloseAsync();
            linked = false;
        }
        finally
        {
            // Safety net: if the test died anywhere between Link and the restore
            // above, Sales is still linked and would break subsequent runs.
            // Page state at failure time is unknown (could be mid-navigation, on
            // the login page, or behind the login-time popup), so re-establish a
            // known state before cleaning up. Best-effort: log rather than throw,
            // so a cleanup failure doesn't mask the original test failure.
            if (linked)
            {
                try
                {
                    if (!await _mainPage.IsLoggedIn())
                    {
                        await _loginPage.NavigateTo();
                        await _loginPage.Login(TestConstants.AdminUser);
                    }
                    if (await _mainPage.IsRoleChooserPopupVisible())
                    {
                        await _mainPage.AcceptRoleChooserSelectingAllAsync();
                    }
                    await _mainPage.NavigateAsync(TestConstants.NavDefault, "Users");
                    await _mainPage.OpenListViewRowAsync(TestConstants.RegularUser);
                    if (await _mainPage.RolesGridRow(TestConstants.RoleSales).IsVisibleAsync())
                    {
                        await _mainPage.UnlinkObjectAsync(TestConstants.RoleSales);
                        await _mainPage.SaveAndCloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine(
                        $"Cleanup failed to restore seed state — Sales may still be linked to {TestConstants.RegularUser}: {ex}");
                }
            }
        }
    }
}
