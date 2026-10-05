using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AlbumsPage(IPage page) : BasePage(page, "albums")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));

    /// <summary>
    /// Clicks the album's name link and waits for its detail page to load.
    /// </summary>
    public async Task<AlbumDetailsPage> GoToAlbumDetailsAsync(string albumName)
    {
        await Collection.GoToDetailsByCellTextAsync("name", albumName);
        var details = new AlbumDetailsPage(Page);
        await details.WaitForLoadedAsync();
        return details;
    }

    /// <summary>
    /// Opens the "New album" dialog from the toolbar.
    /// </summary>
    public async Task<CreateAlbumModalComponent> OpenCreateAlbumAsync()
    {
        await Root.GetByTestId("create-album").ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var modal = new CreateAlbumModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "New Album" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// Opens the editor of an album from its row's context menu.
    /// </summary>
    public async Task<AlbumEditorModalComponent> OpenEditAlbumAsync(string albumName)
    {
        await Collection.ClickRowActionAsync("name", albumName, "Edit Album");

        // The modal is rendered in a portal, outside the page root
        var modal = new AlbumEditorModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "Edit Album" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// Selects several albums and opens their editor from the selection's Actions menu.
    /// </summary>
    public async Task<AlbumEditorModalComponent> OpenEditAlbumsAsync(params string[] albumNames)
    {
        await Collection.SelectRowsByCellTextAsync("name", "songsCount", albumNames);

        var menu = await Collection.OpenFloatingActionsMenuAsync();
        await menu.ClickItemAsync(new Regex(@"^Edit \d+ Albums$"));

        // The modal is rendered in a portal, outside the page root
        var modal = new AlbumEditorModalComponent(
            Page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex(@"^Edit \d+ Albums$") }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// Opens the deletion dialog of an album from its row's context menu, once the dialog knows which songs the
    /// deletion affects.
    /// </summary>
    public async Task<EntityDeleteDialogComponent> OpenDeleteAlbumAsync(string albumName)
    {
        await Collection.ClickRowActionAsync("name", albumName, "Delete Album");

        // The modal is rendered in a portal, outside the page root
        var dialog = new EntityDeleteDialogComponent(
            Page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Album" }), "album");
        await dialog.WaitForLoadedAsync();
        return dialog;
    }

    /// <summary>
    /// Selects several albums and opens their deletion dialog from the selection's Actions menu, once the dialog
    /// knows which songs the deletion affects.
    /// </summary>
    public async Task<EntityDeleteDialogComponent> OpenDeleteAlbumsAsync(params string[] albumNames)
    {
        await Collection.SelectRowsByCellTextAsync("name", "songsCount", albumNames);

        var menu = await Collection.OpenFloatingActionsMenuAsync();
        await menu.ClickItemAsync(new Regex(@"^Delete \d+ Albums$"));

        // The modal is rendered in a portal, outside the page root
        var dialog = new EntityDeleteDialogComponent(
            Page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex(@"^Delete \d+ Albums$") }), "album");
        await dialog.WaitForLoadedAsync();
        return dialog;
    }
}
