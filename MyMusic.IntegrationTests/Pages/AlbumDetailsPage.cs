using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AlbumDetailsPage(IPage page) : BasePage(page, "album-detail")
{
    public SongsCollectionComponent Songs => new(Root.GetByTestId("collection"));

    public ILocator SongsCount => Root.GetByTestId("album-songs-count");

    public ILocator Artist => Root.GetByTestId("album-artist");

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    /// <summary>
    /// The Delete button; absent when the album belongs to another user (a shared view).
    /// </summary>
    public ILocator DeleteButton => Root.GetByTestId("album-delete");

    /// <summary>
    /// Opens the deletion dialog of the album, once the dialog knows which songs the deletion affects.
    /// </summary>
    public async Task<EntityDeleteDialogComponent> OpenDeleteAsync()
    {
        await DeleteButton.ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var dialog = new EntityDeleteDialogComponent(
            Page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Album" }), "album");
        await dialog.WaitForLoadedAsync();
        return dialog;
    }
}
