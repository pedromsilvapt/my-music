using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Resolves the group of soundalikes holding <paramref name="keptSongTitle"/> from the soundalike audit page: keeps
/// that song, applies <paramref name="otherSongsActions"/> to the other songs (by title; the ones left out are
/// deleted), and waits for the resolution to be applied. The resolution is applied from the page's toolbar, which
/// resolves every group with a song to keep, unless <paramref name="groupOnly"/> asks for the group's own button.
/// </summary>
public class ResolveSoundalikesFlow(
    string keptSongTitle,
    Dictionary<string, SoundalikeAction>? otherSongsActions = null,
    bool groupOnly = false) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var audits = await new HomePage(page).Navbar.GoToAuditsAsync();
        var auditDetails = await audits.OpenRuleAsync(AuditsPage.SoundalikeRuleName);
        await auditDetails.Soundalikes.WaitForLoadedAsync();

        var group = auditDetails.Soundalikes.GroupWithSong(keptSongTitle);
        await group.SelectPrimaryAsync(keptSongTitle);

        foreach (var (title, action) in otherSongsActions ?? [])
        {
            await group.SetActionAsync(title, action);
        }

        if (groupOnly)
        {
            await auditDetails.ResolveSoundalikeGroupAsync(group);
        }
        else
        {
            await auditDetails.ResolveSoundalikesAsync();
        }

        await group.WaitForResolvedAsync();
    }
}
