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
        // XAF Blazor uses DevExpress text edit inputs
        var usernameInput = Page.Locator("input.dxbl-text-edit-input[type='text']").First;
        await usernameInput.FillAsync(username);
        if (!string.IsNullOrEmpty(password))
        {
            var passwordInput = Page.Locator("input.dxbl-text-edit-input[type='password']").First;
            await passwordInput.FillAsync(password);
        }
        await Page.ClickAsync("button.xaf-action[data-action-name='Log In']:not([dxbl-virtual-el])");
        await WaitForXafReady();
    }

    public async Task<bool> IsLoginPageVisible()
    {
        return await Page.Locator("input.dxbl-text-edit-input[type='text']").First.IsVisibleAsync();
    }
}
