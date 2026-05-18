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

    public async Task SaveAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
