using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages;

public class AuditsPage(IPage page) : BasePage(page, "audits")
{
    public const string SoundalikeRuleName = "Duplicate Songs (Soundalike)";

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    /// <summary>
    /// Opens the page of the audit rule with the given name.
    /// </summary>
    public async Task<AuditDetailsPage> OpenRuleAsync(string ruleName)
    {
        await Root.GetByRole(AriaRole.Link).Filter(new() { HasText = ruleName }).ClickAsync();
        var page = new AuditDetailsPage(Page);
        await page.WaitForLoadedAsync();
        return page;
    }
}
