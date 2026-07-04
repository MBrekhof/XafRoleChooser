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

    /// <summary>
    /// Log Off lives in the account dropdown. The account button has no text —
    /// only title/aria-label with the username — so target data-action-name='Account'.
    /// </summary>
    public async Task Logout()
    {
        await Page.Locator("button[data-action-name='Account']").First.ClickAsync();
        var logOff = Page.Locator("button:has-text('Log Off'):not([dxbl-virtual-el])").First;
        await logOff.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await logOff.ClickAsync();
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
        // DevExpress renders a hidden [dxbl-virtual-el] twin of every toolbar button
        await popup.Locator("button:has-text('Cancel'):not([dxbl-virtual-el])").ClickAsync();
        await WaitForXafReady();
    }

    // ---- Navigation / CRUD helpers (used by the ROLE-001 regression test) ----

    /// <summary>
    /// Expands the given nav sidebar group (if collapsed) and clicks the named item.
    /// Force-clicks: an .xaf-navigation-link-click-area overlay intercepts pointer
    /// events over the text spans, so normal actionability checks never succeed.
    /// </summary>
    public async Task NavigateAsync(string groupName, string itemName)
    {
        var item = Page.Locator($".xaf-accordion >> text='{itemName}'").First;
        if (!await item.IsVisibleAsync())
        {
            var group = Page.Locator($".xaf-accordion >> text='{groupName}'").First;
            await group.ClickAsync(new() { Force = true });
            await Page.WaitForTimeoutAsync(500);
        }
        await item.ClickAsync(new() { Force = true });
        await WaitForXafReady();
    }

    public async Task OpenListViewRowAsync(string rowText)
    {
        // Gridcell by accessible name — tr:has-text would match the header row too
        await Page.GetByRole(AriaRole.Gridcell, new() { Name = rowText, Exact = true }).First.ClickAsync();
        await WaitForXafReady();
    }

    /// <summary>Locator for a row in the currently-visible Roles collection editor.</summary>
    public ILocator RolesGridRow(string roleName) => Page.Locator($"tr:has-text('{roleName}')").First;

    public async Task LinkObjectAsync(string roleName)
    {
        await Page.Locator("button.xaf-action[data-action-name='Link']:not([dxbl-virtual-el])").ClickAsync();
        var popup = Page.Locator("dxbl-popup-root").Last;
        var row = popup.Locator($"tr:has-text('{roleName}')").First;
        await row.Locator("td.dxbl-grid-selection-cell").ClickAsync();
        await popup.Locator("button:has-text('OK'):not([dxbl-virtual-el])").ClickAsync();
        await WaitForXafReady();
    }

    public async Task UnlinkObjectAsync(string roleName)
    {
        var row = RolesGridRow(roleName);
        await row.Locator("td.dxbl-grid-selection-cell").ClickAsync();
        await Page.Locator("button.xaf-action[data-action-name='Unlink']:not([dxbl-virtual-el])").ClickAsync();

        // Unlink always raises a Yes/No confirmation dialog
        var confirmYes = Page.Locator("button:has-text('Yes'):not([dxbl-virtual-el])").First;
        await confirmYes.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await confirmYes.ClickAsync();
        await WaitForXafReady();
    }

    public async Task SaveAndCloseAsync()
    {
        // Save has xaf-primary-toolbar-btn (not xaf-action) — select by data-action-name only.
        // Link/Unlink on a view-mode DetailView commit immediately, leaving Save disabled —
        // only click when there is actually a pending change.
        var save = Page.Locator("button[data-action-name='Save']:not([dxbl-virtual-el])").First;
        if (await save.IsEnabledAsync())
        {
            await save.ClickAsync();
        }
        await WaitForXafReady();
    }
}
