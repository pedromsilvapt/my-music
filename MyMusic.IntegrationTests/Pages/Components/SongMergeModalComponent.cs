using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The merge songs dialog, opened from the "Merge" action of a songs selection. Shows the selected songs as a
/// soundalike group, whatever their match score, and merges them into the song picked to keep.
/// </summary>
public class SongMergeModalComponent(ILocator root) : BaseComponent(root)
{
    private ILocator Content => Root.GetByTestId("song-merge");

    /// <summary>
    /// The selected songs, with their match score.
    /// </summary>
    public SoundalikeGroupComponent Group => new(Root.GetByTestId("soundalike-group"));

    public async Task WaitForLoadedAsync()
    {
        await Assertions.Expect(Content).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 15000 });
    }

    /// <summary>
    /// Merges the songs into the one picked to keep, and waits for the dialog to close.
    /// </summary>
    public async Task ConfirmAsync()
    {
        await Root.GetByTestId("song-merge-confirm").ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 15000 });
    }
}
