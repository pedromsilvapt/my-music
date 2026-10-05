using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The dialog that merges the selected albums or artists into one of them. It looks up what merging into the
/// chosen <paramref name="entity"/> ("album" or "artist") changes, and says so before the merge is confirmed.
/// </summary>
public class EntityMergeModalComponent(ILocator root, string entity) : BaseComponent(root)
{
    private ILocator Content => Root.GetByTestId($"{entity}-merge");

    /// <summary>
    /// The picker of the album or artist to keep, among the selected ones.
    /// </summary>
    public VirtualSelectComponent Target => new(Root.GetByTestId($"{entity}-merge-target"));

    /// <summary>
    /// What the merge changes; absent while the merge cannot go ahead.
    /// </summary>
    public ILocator Summary => Root.GetByTestId($"{entity}-merge-summary");

    /// <summary>
    /// The reason the merge cannot go ahead, or could not be done.
    /// </summary>
    public ILocator Error => Root.GetByTestId($"{entity}-merge-error");

    public ILocator ConfirmButton => Root.GetByTestId($"{entity}-merge-confirm");

    /// <summary>
    /// Waits for the dialog to show, and for it to know what the merge changes.
    /// </summary>
    public async Task WaitForLoadedAsync()
    {
        await Content.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Assertions.Expect(Content).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    /// <summary>
    /// Chooses the album or artist to keep, and waits for the dialog to know what merging into it changes.
    /// </summary>
    public async Task KeepAsync(string name)
    {
        // The dialog may already keep it by default: typing the name the picker shows would not open its options
        if (await Target.GetValueAsync() != name)
        {
            await Target.SelectAsync(name);
        }

        await WaitForLoadedAsync();
    }

    /// <summary>
    /// The sentences of the summary of what the merge changes, one per line.
    /// </summary>
    public async Task<string[]> GetSummaryAsync() =>
        (await Summary.InnerTextAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The reason the dialog gives for not allowing the merge, once it is sure the merge cannot be confirmed.
    /// </summary>
    public async Task<string> GetRejectionAsync()
    {
        await Error.WaitForAsync(new() { Timeout = 15000 });
        await Assertions.Expect(ConfirmButton).ToBeDisabledAsync();
        return (await Error.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Confirms the merge, and waits for the dialog to close: it stays open until the server is done.
    /// </summary>
    public async Task ConfirmAsync()
    {
        await ConfirmButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 30000 });
    }
}
