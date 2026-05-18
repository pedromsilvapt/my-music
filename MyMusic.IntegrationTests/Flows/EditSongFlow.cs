using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

public class EditSongFlow : IFlow
{
    private readonly string? _songTitle;
    private readonly int? _rowIndex;
    private readonly EditSongOptions _edit;

    public EditSongFlow(string songTitle, EditSongOptions edit)
    {
        _songTitle = songTitle;
        _edit = edit;
    }

    public EditSongFlow(int rowIndex, EditSongOptions edit)
    {
        _rowIndex = rowIndex;
        _edit = edit;
    }

    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = _rowIndex.HasValue
            ? await new OpenSongDetailsFlow(_rowIndex.Value).ExecuteAsync(page)
            : await new OpenSongDetailsFlow(_songTitle!).ExecuteAsync(page);

        var editModal = await songDetails.OpenEditModalAsync();

        if (_edit.Title is not null)
        {
            await editModal.SetTitleAsync(_edit.Title);
        }

        if (_edit.Year is not null)
        {
            await editModal.SetYearAsync(_edit.Year.Value);
        }

        if (_edit.Lyrics is not null)
        {
            await editModal.SetLyricsAsync(_edit.Lyrics);
        }

        if (_edit.Rating is not null)
        {
            await editModal.SetRatingAsync(_edit.Rating.Value);
        }

        if (_edit.Explicit is not null)
        {
            await editModal.SetExplicitAsync(_edit.Explicit.Value);
        }

        if (_edit.Album is not null)
        {
            await editModal.SetAlbumAsync(_edit.Album);
        }

        if (_edit.Artists is not null)
        {
            await editModal.SetArtistsAsync(_edit.Artists);
        }

        if (_edit.AlbumArtist is not null)
        {
            await editModal.SetAlbumArtistAsync(_edit.AlbumArtist);
        }

        await editModal.SaveAsync();
    }
}
