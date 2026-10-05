using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The artist editor, opened for one artist or for several at once. With several, it shows one artist at a time.
/// </summary>
public class ArtistEditorModalComponent(ILocator root) : BaseComponent(root)
{
    public ILocator NameInput => Root.GetByTestId("artist-editor-name");

    public ILocator SubmitButton => Root.GetByTestId("artist-editor-submit");

    /// <summary>
    /// The button that steps to the next artist; only present when the editor was opened for several artists.
    /// </summary>
    public ILocator NextButton => Root.GetByTestId("artist-editor-pager-next");

    /// <summary>
    /// The reason the artists could not be saved, shown while the dialog stays open.
    /// </summary>
    public ILocator Error => Root.GetByTestId("artist-editor-error");

    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// The name of the artist currently shown, as typed so far.
    /// </summary>
    public Task<string> GetNameAsync() => NameInput.InputValueAsync();

    /// <summary>
    /// Renames the artist currently shown.
    /// </summary>
    public Task FillAsync(string name) => NameInput.FillAsync(name);

    /// <summary>
    /// Steps to the next artist, when there is one. Returns whether it did.
    /// </summary>
    public async Task<bool> TryGoToNextAsync()
    {
        if (await NextButton.CountAsync() == 0 || await NextButton.IsDisabledAsync())
        {
            return false;
        }

        await NextButton.ClickAsync();
        return true;
    }

    /// <summary>
    /// Saves every changed artist, and waits for the dialog to close: it stays open until the server is done.
    /// </summary>
    public async Task SaveAsync()
    {
        await SubmitButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 30000 });
    }

    /// <summary>
    /// Saves expecting the server to refuse, and returns the error the dialog shows.
    /// </summary>
    public async Task<string> SaveRejectedAsync()
    {
        await SubmitButton.ClickAsync();
        await Error.WaitForAsync(new() { Timeout = 15000 });
        return (await Error.InnerTextAsync()).Trim();
    }
}
