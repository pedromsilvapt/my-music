using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The album editor, opened for one album or for several at once. With several, it shows one album at a time.
/// </summary>
public class AlbumEditorModalComponent(ILocator root) : BaseComponent(root)
{
    public ILocator NameInput => Root.GetByTestId("album-editor-name");

    public ILocator YearInput => Root.GetByTestId("album-editor-year");

    public ILocator SubmitButton => Root.GetByTestId("album-editor-submit");

    /// <summary>
    /// The button that steps to the next album; only present when the editor was opened for several albums.
    /// </summary>
    public ILocator NextButton => Root.GetByTestId("album-editor-pager-next");

    /// <summary>
    /// The reason the albums could not be saved, shown while the dialog stays open.
    /// </summary>
    public ILocator Error => Root.GetByTestId("album-editor-error");

    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// The name of the album currently shown, as typed so far.
    /// </summary>
    public Task<string> GetNameAsync() => NameInput.InputValueAsync();

    /// <summary>
    /// Changes the album currently shown: a <see langword="null"/> value leaves that field as it is.
    /// </summary>
    public async Task FillAsync(string? name = null, int? year = null)
    {
        if (name is not null)
        {
            await NameInput.FillAsync(name);
        }

        if (year is not null)
        {
            await YearInput.FillAsync(year.Value.ToString());
        }
    }

    /// <summary>
    /// Steps to the next album, when there is one. Returns whether it did.
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
    /// Saves every changed album, and waits for the dialog to close: it stays open until the server is done.
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
