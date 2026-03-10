using Microsoft.Playwright;

namespace XafRoleChooser.Playwright.Infrastructure;

public class XafPageBase
{
    protected readonly IPage Page;

    public XafPageBase(IPage page)
    {
        Page = page;
    }

    public async Task WaitForXafReady()
    {
        // Wait for XAF Blazor to finish loading
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // Wait for the loading indicator to disappear
        var spinner = Page.Locator(".dx-blazor-loading-panel, .xaf-loading");
        if (await spinner.IsVisibleAsync())
        {
            await spinner.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = TestConstants.DefaultTimeout });
        }
    }
}
