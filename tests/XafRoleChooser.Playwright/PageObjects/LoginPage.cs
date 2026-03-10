using Microsoft.Playwright;

namespace XafRoleChooser.Playwright.PageObjects;

public class LoginPage : Infrastructure.XafPageBase
{
    public LoginPage(IPage page) : base(page) { }

    public async Task NavigateTo()
    {
        await Page.GotoAsync($"{Infrastructure.TestConstants.BaseUrl}/LoginPage");
        await WaitForXafReady();
    }

    public async Task Login(string username, string password = "")
    {
        await Page.FillAsync("input[placeholder='User Name'], input#UserName", username);
        if (!string.IsNullOrEmpty(password))
        {
            await Page.FillAsync("input[placeholder='Password'], input#Password, input[type='password']", password);
        }
        await Page.ClickAsync("button:has-text('Log In'), .xaf-logon-button, button[type='submit']");
        await WaitForXafReady();
    }

    public async Task<bool> IsLoginPageVisible()
    {
        return await Page.Locator("input[placeholder='User Name'], input#UserName").IsVisibleAsync();
    }
}
