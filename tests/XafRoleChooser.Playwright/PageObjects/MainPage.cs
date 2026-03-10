using Microsoft.Playwright;

namespace XafRoleChooser.Playwright.PageObjects;

public class MainPage : Infrastructure.XafPageBase
{
    public MainPage(IPage page) : base(page) { }

    public async Task<bool> IsLoggedIn()
    {
        var mainView = Page.Locator(".xaf-chrome, .xaf-tabbed-mdi");
        return await mainView.First.IsVisibleAsync();
    }

    public async Task ClickActiveRolesButton()
    {
        // The Active Roles button is on the Tools tab in the XAF Blazor ribbon
        var toolsTab = Page.GetByText("Tools", new() { Exact = true }).First;
        await toolsTab.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        await Page.Locator("button[data-action-name='Active Roles']").ClickAsync();
        await WaitForXafReady();
    }

    public async Task SelectRoleInChooser(string roleName)
    {
        // Scope to the popup modal to avoid matching background page elements
        var popup = Page.Locator("dxbl-popup-root");
        var row = popup.Locator($"tr:has-text('{roleName}')").First;
        var selectionCell = row.Locator("td.dxbl-grid-selection-cell");
        await selectionCell.ClickAsync();
        await Page.WaitForTimeoutAsync(300);
    }

    public async Task<bool> IsRoleChooserPopupVisible()
    {
        // Check for the popup dialog title "Active Role Selection"
        var title = Page.GetByText("Active Role Selection", new() { Exact = true });
        try
        {
            await title.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task AcceptRoleChooser()
    {
        await Page.ClickAsync("button:has-text('OK'), button:has-text('Accept'), .dxbs-popup button.btn-primary");
        await WaitForXafReady();
    }

    public async Task<bool> IsNavGroupVisible(string groupName, int timeoutMs = 5000)
    {
        try
        {
            // Scope to xaf-accordion sidebar to avoid matching tab headers or content text
            var navGroup = Page.Locator($".xaf-accordion >> text='{groupName}'").First;
            await navGroup.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<bool> IsNavGroupHidden(string groupName, int timeoutMs = 3000)
    {
        try
        {
            var navGroup = Page.Locator($".xaf-accordion >> text='{groupName}'").First;
            await navGroup.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task WaitForNavigationRefresh()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await WaitForXafReady();
        await Page.WaitForTimeoutAsync(1000);
    }

    public async Task DebugScreenshot(string name)
    {
        await Page.ScreenshotAsync(new() { Path = $"C:/Projects/XafRoleChooser/debug_{name}.png", FullPage = true });
    }

    public async Task Logout()
    {
        await Page.ClickAsync("button:has-text('Log Off'), .xaf-logoff-button, a:has-text('Log Off')");
        await WaitForXafReady();
    }
}
