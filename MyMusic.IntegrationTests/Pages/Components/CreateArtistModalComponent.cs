using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The "New artist" dialog, opened from the artists page toolbar.
/// </summary>
public class CreateArtistModalComponent(ILocator root) : BaseComponent(root)
{
    public ILocator NameInput => Root.GetByTestId("create-artist-name");

    public ILocator SubmitButton => Root.GetByTestId("create-artist-submit");

    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Creates the artist, and waits for the dialog to close.
    /// </summary>
    public async Task CreateAsync(string name)
    {
        await NameInput.FillAsync(name);
        await SubmitButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 15000 });
    }
}
