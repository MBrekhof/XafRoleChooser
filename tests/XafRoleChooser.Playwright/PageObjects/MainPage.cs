using Microsoft.Playwright;

namespace XafRoleChooser.Playwright.PageObjects;

public class MainPage : Infrastructure.XafPageBase
{
    public MainPage(IPage page) : base(page) { }

    public async Task<bool> IsLoggedIn()
    {
        // Check for XAF main view indicators
        var mainView = Page.Locator(".xaf-main-module, .view-content, #ContentPlaceHolder");
        return await mainView.IsVisibleAsync();
    }

    public async Task ClickActiveRolesButton()
    {
        await Page.ClickAsync("button:has-text('Active Roles'), [data-action='ChooseActiveRoles']");
        await WaitForXafReady();
    }

    public async Task<bool> IsRoleChooserPopupVisible()
    {
        var popup = Page.Locator(".dxbs-popup, .dxbl-modal, .dx-popup-wrapper");
        return await popup.IsVisibleAsync();
    }

    public async Task ToggleRoleInChooser(string roleName)
    {
        // Find the row with the role name and click its checkbox
        var row = Page.Locator($"tr:has-text('{roleName}'), .dxbs-grid-row:has-text('{roleName}')");
        var checkbox = row.Locator("input[type='checkbox'], .dxbs-checkbox");
        await checkbox.ClickAsync();
    }

    public async Task AcceptRoleChooser()
    {
        await Page.ClickAsync("button:has-text('OK'), button:has-text('Accept'), .dxbs-popup button.btn-primary");
        await WaitForXafReady();
    }

    public async Task<bool> IsNavigationItemVisible(string itemName)
    {
        var navItem = Page.Locator($".xaf-navigation a:has-text('{itemName}'), .nav-item:has-text('{itemName}')");
        return await navItem.IsVisibleAsync();
    }

    public async Task Logout()
    {
        await Page.ClickAsync("button:has-text('Log Off'), .xaf-logoff-button, a:has-text('Log Off')");
        await WaitForXafReady();
    }
}
