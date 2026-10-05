using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The confirmation dialog shown before deleting an album or an artist. It looks up the songs that still
/// reference the <paramref name="entity"/> ("album" or "artist"), and warns about them.
/// </summary>
public class EntityDeleteDialogComponent(ILocator root, string entity) : BaseComponent(root)
{
    private ILocator Confirmation => Root.GetByTestId($"{entity}-delete-confirmation");

    /// <summary>
    /// The warning about the songs the deletion changes; absent when no song references the album or artist.
    /// </summary>
    public ILocator Warning => Root.GetByTestId($"{entity}-delete-warning");

    /// <summary>
    /// Waits for the dialog to show, and for it to know how many songs the deletion affects.
    /// </summary>
    public async Task WaitForLoadedAsync()
    {
        await Confirmation.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Assertions.Expect(Confirmation).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    /// <summary>
    /// The text of the songs warning, or <see langword="null"/> when the dialog shows none.
    /// </summary>
    public async Task<string?> GetWarningAsync() =>
        await Warning.CountAsync() > 0 ? (await Warning.InnerTextAsync()).Trim() : null;

    /// <summary>
    /// Confirms the deletion, and waits for the dialog to close: it stays open until the server is done.
    /// </summary>
    public async Task ConfirmAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 30000 });
    }
}
