using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The "New album" dialog, opened from the albums page toolbar.
/// </summary>
public class CreateAlbumModalComponent(ILocator root) : BaseComponent(root)
{
    public ILocator NameInput => Root.GetByTestId("create-album-name");

    public VirtualSelectComponent Artist => new(Root.GetByTestId("create-album-artist"));

    public ILocator YearInput => Root.GetByTestId("create-album-year");

    public ILocator SubmitButton => Root.GetByTestId("create-album-submit");

    /// <summary>
    /// The reason the album could not be created, shown while the dialog stays open.
    /// </summary>
    public ILocator Error => Root.GetByTestId("create-album-error");

    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    public async Task FillAsync(string name, string artistName, int? year = null)
    {
        await NameInput.FillAsync(name);
        await Artist.SelectAsync(artistName);

        if (year is not null)
        {
            await YearInput.FillAsync(year.Value.ToString());
        }
    }

    /// <summary>
    /// Submits the dialog, and waits for it to close.
    /// </summary>
    public async Task CreateAsync()
    {
        await SubmitButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 15000 });
    }

    /// <summary>
    /// Submits the dialog expecting the server to reject the album, and returns the error it shows.
    /// </summary>
    public async Task<string> CreateRejectedAsync()
    {
        await SubmitButton.ClickAsync();
        await Error.WaitForAsync(new() { Timeout = 15000 });
        return await Error.InnerTextAsync();
    }
}
