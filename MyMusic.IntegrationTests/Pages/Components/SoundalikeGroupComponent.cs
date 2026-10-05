using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// A group of soundalike songs, in the soundalike audit page or in the merge songs dialog: one song is picked as the
/// one to keep, and each of the others gets an action.
/// </summary>
public class SoundalikeGroupComponent(ILocator root) : BaseComponent(root)
{
    private ILocator Song(string title) => Root.GetByTestId("soundalike-song").Filter(new() { HasText = title });

    /// <summary>
    /// The match score shown for the group, as a percentage, or null when it is shown as unavailable.
    /// </summary>
    public async Task<int?> GetMatchScoreAsync()
    {
        var match = Regex.Match(await Root.GetByTestId("soundalike-match-score").InnerTextAsync(), @"(\d+)%");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    /// <summary>
    /// The action currently chosen for a song that is not kept, or null when it has none.
    /// </summary>
    public async Task<SoundalikeAction?> GetActionAsync(string title)
    {
        foreach (var action in Enum.GetValues<SoundalikeAction>())
        {
            var badge = Song(title).GetByTestId($"soundalike-action-{action.ToString().ToLowerInvariant()}");
            if (await badge.GetAttributeAsync("data-active") == "true")
                return action;
        }

        return null;
    }

    /// <summary>
    /// Picks the song to keep. Every other song of the group then gets the default action.
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
    /// Asks to resolve this group alone, which opens the confirmation dialog. Requires a song to keep to be selected
    /// first.
    /// </summary>
    public async Task ClickResolveAsync()
    {
        await Root.GetByTestId("soundalike-group-resolve").ClickAsync();
    }

    /// <summary>
    /// Waits until the group is gone from the page, once its resolution has been applied.
    /// </summary>
    public async Task WaitForResolvedAsync()
    {
        await Assertions.Expect(Root).ToBeHiddenAsync(new() { Timeout = 15000 });
    }
}
