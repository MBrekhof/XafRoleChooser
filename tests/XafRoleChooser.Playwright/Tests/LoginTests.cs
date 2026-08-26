using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using XafRoleChooser.Playwright.Infrastructure;
using XafRoleChooser.Playwright.PageObjects;

namespace XafRoleChooser.Playwright.Tests;

[TestFixture]
public class LoginTests : PageTest
{
    // Log off after every test so no server-side sticky selection leaks into later fixtures.
    [TearDown]
    public Task TearDown() => new MainPage(Page).ResetSessionAsync();

    [Test]
    public async Task LoginPage_ShouldBeAccessible()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateTo();
        Assert.That(await loginPage.IsLoginPageVisible(), Is.True);
    }

    [Test]
    public async Task Admin_ShouldBeAbleToLogin()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateTo();
        await loginPage.Login(TestConstants.AdminUser);

        var mainPage = new MainPage(Page);
        await mainPage.WaitForXafReady();
        Assert.That(await mainPage.IsLoggedIn(), Is.True);
    }

    [Test]
    public async Task User_ShouldBeAbleToLogin()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.NavigateTo();
        await loginPage.Login(TestConstants.RegularUser);

        var mainPage = new MainPage(Page);
        await mainPage.WaitForXafReady();
        Assert.That(await mainPage.IsLoggedIn(), Is.True);
    }
}
