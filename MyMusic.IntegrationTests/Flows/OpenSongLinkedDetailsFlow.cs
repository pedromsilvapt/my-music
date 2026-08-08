using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

public enum SongLinkedTarget
{
    Album,
    Artist,
}

public record SongLinkedDetailsResult(AlbumDetailsPage? Album, ArtistDetailsPage? Artist);

/// <summary>
/// Opens a song's detail page by name (as a user would, via the Songs list), then clicks
/// the album or artist link on that page to reach the linked details page. The result
/// exposes the matching <see cref="AlbumDetailsPage"/> or <see cref="ArtistDetailsPage"/>
/// for the requested <see cref="SongLinkedTarget"/>.
/// </summary>
public class OpenSongLinkedDetailsFlow(string songTitle, SongLinkedTarget target, int artistIndex = 0)
    : IFlow<SongLinkedDetailsResult>
{
    public async Task<SongLinkedDetailsResult> ExecuteAsync(IPage page)
    {
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);

        if (target == SongLinkedTarget.Album)
        {
            var albumPage = await songDetails.ClickAlbumLinkAsync();
            return new SongLinkedDetailsResult(albumPage, null);
        }

        var artistPage = await songDetails.ClickArtistLinkAsync(artistIndex);
        return new SongLinkedDetailsResult(null, artistPage);
    }
}