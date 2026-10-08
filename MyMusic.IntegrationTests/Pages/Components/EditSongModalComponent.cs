using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class EditSongModalComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task SetTitleAsync(string title)
    {
        await Root.GetByTestId("edit-song-title").FillAsync(title);
    }

    public async Task SetYearAsync(int year)
    {
        await Root.GetByTestId("edit-song-year").FillAsync(year.ToString());
    }

    public async Task SetLyricsAsync(string lyrics)
    {
        await Root.GetByTestId("edit-song-lyrics").FillAsync(lyrics);
    }

    public async Task SetRatingAsync(int rating)
    {
        var ratingInput = Root.GetByTestId("edit-song-rating").Locator("input[type='range']");
        await ratingInput.FillAsync(rating.ToString());
    }

    public async Task SetExplicitAsync(bool isExplicit)
    {
        var explicitInput = Root.GetByTestId("edit-song-explicit");
        var isChecked = await explicitInput.IsCheckedAsync();
        if (isChecked != isExplicit)
        {
            // Mantine renders the Switch <input> as visually hidden (opacity 0).
            // Playwright refuses to click hidden elements, so we click the
            // parent .mantine-Switch-track which is the actual visible toggle.
            await explicitInput.Locator("xpath=..").ClickAsync();
        }
    }

    public async Task SetAlbumAsync(string album)
    {
        var albumInput = Root.GetByTestId("edit-song-album");
        await albumInput.ClearAsync();
        await albumInput.FillAsync(album);
        await albumInput.BlurAsync();
    }

    /// <summary>
    /// Types the album's name and picks, among the suggestions, the album of the given album artist.
    /// </summary>
    public async Task PickAlbumAsync(string album, string albumArtist)
    {
        var albumInput = Root.GetByTestId("edit-song-album");
        await albumInput.ClearAsync();
        await albumInput.FillAsync(album);

        await Root.GetByRole(AriaRole.Option)
            .Filter(new() { HasText = album })
            .Filter(new() { HasText = albumArtist })
            .ClickAsync();
    }

    public async Task SetAlbumArtistAsync(string albumArtist)
    {
        var albumArtistInput = Root.GetByTestId("edit-song-album-artist");
        await albumArtistInput.ClearAsync();
        await albumArtistInput.FillAsync(albumArtist);
        await albumArtistInput.BlurAsync();
    }

    public async Task SetArtistsAsync(string[] artists)
    {
        var artistsInput = Root.GetByTestId("edit-song-artists");

        await artistsInput.ClickAsync();
        await artistsInput.PressAsync("Control+a");
        await artistsInput.PressAsync("Backspace");

        foreach (var artist in artists)
        {
            await artistsInput.FillAsync(artist);
            await artistsInput.PressAsync("Enter");
        }
    }

    /// <summary>
    /// Runs the "Recalculate Checksum" tool from the tools menu. Its outcome is reported in a notification.
    /// </summary>
    public async Task RecalculateChecksumAsync()
    {
        await Root.GetByTestId("edit-song-tools").ClickAsync();

        // The menu is rendered in a portal, outside the dialog
        await Root.Page.GetByTestId("song-tool-recalculate-checksum").ClickAsync();
    }

    /// <summary>
    /// Opens the "Change Timestamps" tool from the tools menu and returns its dialog, once it shows the song's
    /// timestamps.
    /// </summary>
    public async Task<SongTimestampsModalComponent> OpenChangeTimestampsAsync()
    {
        await Root.GetByTestId("edit-song-tools").ClickAsync();

        // The menu and the tool's dialog are rendered in portals, outside the edit dialog
        await Root.Page.GetByTestId("song-tool-change-timestamps").ClickAsync();

        var timestampsModal = new SongTimestampsModalComponent(Root.Page.GetByTestId("song-timestamps-modal"));
        await timestampsModal.WaitForLoadedAsync();
        return timestampsModal;
    }

    /// <summary>
    /// Opens the "Upload Song" tool from the tools menu and returns its dialog.
    /// </summary>
    public async Task<SongFileUploadModalComponent> OpenUploadSongAsync()
    {
        await Root.GetByTestId("edit-song-tools").ClickAsync();

        // The menu and the tool's dialog are rendered in portals, outside the edit dialog
        await Root.Page.GetByTestId("song-tool-upload-song").ClickAsync();

        var uploadModal = new SongFileUploadModalComponent(Root.Page.GetByTestId("song-file-upload-modal"));
        await uploadModal.WaitForOpenedAsync();
        return uploadModal;
    }

    public async Task CancelAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task SaveAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
