using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class ArtistsPage(IPage page) : BasePage(page, "artists")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));

    /// <summary>
    /// Clicks the artist's name link and waits for its detail page to load.
    /// </summary>
    public async Task<ArtistDetailsPage> GoToArtistDetailsAsync(string artistName)
    {
        await Collection.GoToDetailsByCellTextAsync("name", artistName);
        var details = new ArtistDetailsPage(Page);
        await details.WaitForLoadedAsync();
        return details;
    }

    /// <summary>
    /// Opens the "New artist" dialog from the toolbar.
    /// </summary>
    public async Task<CreateArtistModalComponent> OpenCreateArtistAsync()
    {
        await Root.GetByTestId("create-artist").ClickAsync();

        // The modal is rendered in a portal, outside the page root
        var modal = new CreateArtistModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "New Artist" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// Opens the editor of an artist from its row's context menu.
    /// </summary>
    public async Task<ArtistEditorModalComponent> OpenEditArtistAsync(string artistName)
    {
        await Collection.ClickRowActionAsync("name", artistName, "Edit Artist");

        // The modal is rendered in a portal, outside the page root
        var modal = new ArtistEditorModalComponent(Page.GetByRole(AriaRole.Dialog, new() { Name = "Edit Artist" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// Selects several artists and opens their editor from the selection's Actions menu.
    /// </summary>
    public async Task<ArtistEditorModalComponent> OpenEditArtistsAsync(params string[] artistNames)
    {
        await Collection.SelectRowsByCellTextAsync("name", "songsCount", artistNames);

        var menu = await Collection.OpenFloatingActionsMenuAsync();
        await menu.ClickItemAsync(new Regex(@"^Edit \d+ Artists$"));

        // The modal is rendered in a portal, outside the page root
        var modal = new ArtistEditorModalComponent(
            Page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex(@"^Edit \d+ Artists$") }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// Opens the deletion dialog of an artist from its row's context menu, once the dialog knows which songs the
    /// deletion affects.
    /// </summary>
    public async Task<EntityDeleteDialogComponent> OpenDeleteArtistAsync(string artistName)
    {
        await Collection.ClickRowActionAsync("name", artistName, "Delete Artist");

        // The modal is rendered in a portal, outside the page root
        var dialog = new EntityDeleteDialogComponent(
            Page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Artist" }), "artist");
        await dialog.WaitForLoadedAsync();
        return dialog;
    }

    /// <summary>
    /// Selects several artists and opens their deletion dialog from the selection's Actions menu, once the dialog
    /// knows which songs the deletion affects.
    /// </summary>
    public async Task<EntityDeleteDialogComponent> OpenDeleteArtistsAsync(params string[] artistNames)
    {
        await Collection.SelectRowsByCellTextAsync("name", "songsCount", artistNames);

        var menu = await Collection.OpenFloatingActionsMenuAsync();
        await menu.ClickItemAsync(new Regex(@"^Delete \d+ Artists$"));

        // The modal is rendered in a portal, outside the page root
        var dialog = new EntityDeleteDialogComponent(
            Page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex(@"^Delete \d+ Artists$") }), "artist");
        await dialog.WaitForLoadedAsync();
        return dialog;
    }

    /// <summary>
    /// Selects several artists and opens their merge dialog from the selection's Actions menu, once the dialog knows
    /// what merging into the artist it keeps by default changes.
    /// </summary>
    public async Task<EntityMergeModalComponent> OpenMergeArtistsAsync(params string[] artistNames)
    {
        await Collection.SelectRowsByCellTextAsync("name", "songsCount", artistNames);

        var menu = await Collection.OpenFloatingActionsMenuAsync();
        await menu.ClickItemAsync(new Regex(@"^Merge \d+ Artists$"));

        // The modal is rendered in a portal, outside the page root
        var modal = new EntityMergeModalComponent(
            Page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex(@"^Merge \d+ Artists$") }), "artist");
        await modal.WaitForLoadedAsync();
        return modal;
    }
}
