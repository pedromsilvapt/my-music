using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// A group of soundalike songs in the soundalike audit page: one song is picked as the one to keep, and each of the
/// others gets an action (delete by default).
/// </summary>
public class SoundalikeGroupComponent(ILocator root) : BaseComponent(root)
{
    private ILocator Song(string title) => Root.GetByTestId("soundalike-song").Filter(new() { HasText = title });

    /// <summary>
    /// Picks the song to keep. Every other song of the group is then set to be deleted.
    /// </summary>
    public async Task SelectPrimaryAsync(string title)
    {
        var song = Song(title);
        await song.ClickAsync();
        await Assertions.Expect(song).ToHaveAttributeAsync("data-primary", "true");
    }

    /// <summary>
    /// Chooses what happens to a song that is not kept. Requires a song to keep to be selected first.
    /// </summary>
    public async Task SetActionAsync(string title, SoundalikeAction action)
    {
        var badge = Song(title).GetByTestId($"soundalike-action-{action.ToString().ToLowerInvariant()}");

        if (await badge.GetAttributeAsync("data-active") != "true")
        {
            await badge.ClickAsync();
        }

        await Assertions.Expect(badge).ToHaveAttributeAsync("data-active", "true");
    }

    /// <summary>
    /// Waits until the group is gone from the page, once its resolution has been applied.
    /// </summary>
    public async Task WaitForResolvedAsync()
    {
        await Assertions.Expect(Root).ToBeHiddenAsync(new() { Timeout = 15000 });
    }
}
