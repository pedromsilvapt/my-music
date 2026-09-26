using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class PlaylistDetailsPage(IPage page) : BasePage(page, "playlist-detail")
{
    public SongsCollectionComponent Songs => new(Root.GetByTestId("collection"));

    public PlaylistShareIndicatorComponent ShareIndicator => new(Root.GetByTestId("playlist-share-indicator").First);

    public ILocator SharedByLabel => Root.GetByTestId("playlist-shared-by");

    public ILocator ShareButton => Root.GetByRole(AriaRole.Button, new() { Name = "Share" });

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    public async Task<ManageSharingDialogComponent> OpenShareDialogAsync()
    {
        await ShareButton.ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();
        var sharingDialog = new ManageSharingDialogComponent(dialog);
        await sharingDialog.WaitForLoadedAsync();
        return sharingDialog;
    }
}
