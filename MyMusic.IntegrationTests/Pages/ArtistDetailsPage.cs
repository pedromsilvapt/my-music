using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class ArtistDetailsPage(IPage page) : BasePage(page, "artist-detail")
{
    private ILocator Collections => Root.GetByTestId("collection");

    public ILocator SongsCount => Root.GetByTestId("artist-songs-count");

    public ILocator AlbumsCount => Root.GetByTestId("artist-albums-count");

    public ILocator Name => Root.GetByTestId("artist-name");

    /// <summary>
    /// The Songs collection on the artist detail page. The Albums collection (if present) renders
    /// as a grid without song title cells, so the Songs collection is the one containing song rows.
    /// Falls back to the last collection when no song rows are found (e.g. empty song list still loading).
    /// </summary>
    public SongsCollectionComponent Songs
    {
        get
        {
            var withSongRows = Collections.Filter(new()
            {
                Has = Root.Page.Locator("td[data-testid^='collection-cell-title-']"),
            });
            return new SongsCollectionComponent(withSongRows.Nth(0));
        }
    }

    /// <summary>
    /// The albums of the artist with the given name, as listed in the Albums collection (the first one of the page).
    /// </summary>
    public ILocator GetAlbums(string albumName) =>
        Collections.First.Locator("[data-index]").Filter(new() { HasText = albumName });

    /// <summary>
    /// The "New album" button; absent when the artist belongs to another user (a shared view).
    /// </summary>
    public ILocator CreateAlbumButton => Root.GetByTestId("create-album");

    /// <summary>
    /// Opens the "New album" dialog, which starts with this artist picked.
    /// </summary>
    public async Task<CreateAlbumModalComponent> OpenCreateAlbumAsync()
    {
        await CreateAlbumButton.ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var modal = new CreateAlbumModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "New Album" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Root).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    /// <summary>
    /// The Edit button; absent when the artist belongs to another user (a shared view).
    /// </summary>
    public ILocator EditButton => Root.GetByTestId("artist-edit");

    /// <summary>
    /// Opens the editor of the artist.
    /// </summary>
    public async Task<ArtistEditorModalComponent> OpenEditAsync()
    {
        await EditButton.ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var modal = new ArtistEditorModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "Edit Artist" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// The Delete button; absent when the artist belongs to another user (a shared view).
    /// </summary>
    public ILocator DeleteButton => Root.GetByTestId("artist-delete");

    /// <summary>
    /// Opens the deletion dialog of the artist, once the dialog knows which songs the deletion affects.
    /// </summary>
    public async Task<EntityDeleteDialogComponent> OpenDeleteAsync()
    {
        await DeleteButton.ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var dialog = new EntityDeleteDialogComponent(
            Page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Artist" }), "artist");
        await dialog.WaitForLoadedAsync();
        return dialog;
    }
}
