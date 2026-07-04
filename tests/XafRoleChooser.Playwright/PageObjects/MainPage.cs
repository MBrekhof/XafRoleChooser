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

    // ---- Login-time interstitial helpers ----

    public async Task WaitForRoleChooserPopupAsync()
    {
        var title = Page.GetByText("Active Role Selection", new() { Exact = true });
        await title.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = Infrastructure.TestConstants.DefaultTimeout });
    }

    /// <summary>
    /// Waits for the chooser popup, selects every role row via the header "select all"
    /// checkbox, and accepts. Used by tests that only need all roles active afterwards
    /// (e.g. the ROLE-001 regression test) without caring about the narrowing behavior.
    /// </summary>
    public async Task AcceptRoleChooserSelectingAllAsync()
    {
        await WaitForRoleChooserPopupAsync();
        var popup = Page.Locator("dxbl-popup-root");
        await popup.Locator("th.dxbl-grid-selection-cell, td.dxbl-grid-selection-cell").First.ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        await AcceptRoleChooser();
    }

    public async Task CancelRoleChooserAsync()
    {
        var popup = Page.Locator("dxbl-popup-root");
        await popup.Locator("button:has-text('Cancel')").ClickAsync();
        await WaitForXafReady();
    }

    // ---- Navigation / CRUD helpers (used by the ROLE-001 regression test) ----

    /// <summary>
    /// Expands the given nav sidebar group (if collapsed) and clicks the named item.
    /// </summary>
    public async Task NavigateAsync(string groupName, string itemName)
    {
        var item = Page.Locator($".xaf-accordion >> text='{itemName}'").First;
        if (!await item.IsVisibleAsync())
        {
            var group = Page.Locator($".xaf-accordion >> text='{groupName}'").First;
            await group.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }
        await item.ClickAsync();
        await WaitForXafReady();
    }

    public async Task OpenListViewRowAsync(string rowText)
    {
        await Page.Locator($"tr:has-text('{rowText}')").First.ClickAsync();
        await WaitForXafReady();
    }

    /// <summary>Locator for a row in the currently-visible Roles collection editor.</summary>
    public ILocator RolesGridRow(string roleName) => Page.Locator($"tr:has-text('{roleName}')").First;

    public async Task LinkObjectAsync(string roleName)
    {
        await Page.Locator("button.xaf-action[data-action-name='Link']").ClickAsync();
        var popup = Page.Locator("dxbl-popup-root").Last;
        var row = popup.Locator($"tr:has-text('{roleName}')").First;
        await row.Locator("td.dxbl-grid-selection-cell").ClickAsync();
        await popup.Locator("button:has-text('OK')").ClickAsync();
        await WaitForXafReady();
    }

    public async Task UnlinkObjectAsync(string roleName)
    {
        var row = RolesGridRow(roleName);
        await row.Locator("td.dxbl-grid-selection-cell").ClickAsync();
        await Page.Locator("button.xaf-action[data-action-name='Unlink']").ClickAsync();

        var confirmYes = Page.Locator("button:has-text('Yes')");
        await confirmYes.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await confirmYes.ClickAsync();
        await WaitForXafReady();
    }

    public async Task SaveAndCloseAsync()
    {
        await Page.Locator("button.xaf-action[data-action-name='Save']").ClickAsync();
        await WaitForXafReady();
    }
}
