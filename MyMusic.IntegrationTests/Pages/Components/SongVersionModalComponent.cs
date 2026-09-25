using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Models;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The read-only song version modal, opened from the song detail page's versions menu. Shows a revision
/// header with previous/next navigation, and side-by-side JSON panels with the fields changed by that
/// revision (old on the left, new on the right). The first revision has no old panel.
/// </summary>
public class SongVersionModalComponent(ILocator root) : BaseComponent(root)
{
    private ILocator Content => Root.GetByTestId("song-version-modal");
    private ILocator Header => Root.GetByTestId("song-version-header");
    private ILocator OldPanel => Root.GetByTestId("song-version-old");
    private ILocator NewPanel => Root.GetByTestId("song-version-new");
    private ILocator PreviousButton => Root.GetByTestId("song-version-previous");
    private ILocator NextButton => Root.GetByTestId("song-version-next");

    public async Task WaitForLoadedAsync()
    {
        await Assertions.Expect(Content).ToHaveAttributeAsync("data-loading", "false");
    }

    /// <summary>
    /// The revision number shown in the header (e.g. 3 for "Revision 3 - 2 minutes ago").
    /// </summary>
    public async Task<int> GetRevisionAsync()
    {
        return int.Parse(Regex.Match(await Header.InnerTextAsync(), @"\d+").Value);
    }

    /// <summary>
    /// The values shown in the old panel, or null when viewing the first revision.
    /// </summary>
    public async Task<SongVersionValues?> GetOldValuesAsync()
    {
        return await OldPanel.CountAsync() > 0 ? SongVersionValues.Parse(await OldPanel.InnerTextAsync()) : null;
    }

    /// <summary>
    /// The values shown in the new panel.
    /// </summary>
    public async Task<SongVersionValues> GetNewValuesAsync()
    {
        return SongVersionValues.Parse(await NewPanel.InnerTextAsync());
    }

    public async Task<bool> CanGoToPreviousAsync()
    {
        return await PreviousButton.IsEnabledAsync();
    }

    public async Task<bool> CanGoToNextAsync()
    {
        return await NextButton.IsEnabledAsync();
    }

    /// <summary>
    /// Moves to the previous (older) revision and waits for it to load.
    /// </summary>
    public async Task GoToPreviousAsync()
    {
        await NavigateAsync(PreviousButton);
    }

    /// <summary>
    /// Moves to the next (newer) revision and waits for it to load.
    /// </summary>
    public async Task GoToNextAsync()
    {
        await NavigateAsync(NextButton);
    }

    public async Task CloseAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    private async Task NavigateAsync(ILocator button)
    {
        var header = await Header.InnerTextAsync();
        await button.ClickAsync();
        await Assertions.Expect(Header).Not.ToHaveTextAsync(header);
        await WaitForLoadedAsync();
    }
}
