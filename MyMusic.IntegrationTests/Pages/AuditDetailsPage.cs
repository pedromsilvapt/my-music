using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AuditDetailsPage(IPage page) : BasePage(page, "audit-detail")
{
    /// <summary>
    /// The soundalike groups, shown only by the soundalike audit rule.
    /// </summary>
    public SoundalikesComponent Soundalikes => new(Root.GetByTestId("soundalikes"));

    private ILocator RemoveDuplicatesButton =>
        Root.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Remove Duplicates") });

    private ILocator ConfirmDialog => Page.GetByRole(AriaRole.Dialog);

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    /// <summary>
    /// Applies the resolution of every soundalike group with a song selected to keep, confirming the dialog.
    /// </summary>
    public async Task ResolveSoundalikesAsync()
    {
        await RemoveDuplicatesButton.ClickAsync();
        await ConfirmDialog.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Resolve") }).ClickAsync();
        await ConfirmDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
