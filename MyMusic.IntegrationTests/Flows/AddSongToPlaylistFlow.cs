using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

public class AddSongToPlaylistFlow : IFlow
{
    private readonly string? _songTitle;
    private readonly int? _rowIndex;
    private readonly string _playlistName;
    private readonly bool _createPlaylistIfNotExists;

    public AddSongToPlaylistFlow(string songTitle, string playlistName, bool createPlaylistIfNotExists = true)
    {
        _songTitle = songTitle;
        _playlistName = playlistName;
        _createPlaylistIfNotExists = createPlaylistIfNotExists;
    }

    public AddSongToPlaylistFlow(int rowIndex, string playlistName, bool createPlaylistIfNotExists = true)
    {
        _rowIndex = rowIndex;
        _playlistName = playlistName;
        _createPlaylistIfNotExists = createPlaylistIfNotExists;
    }

    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = _rowIndex.HasValue
            ? await new OpenSongDetailsFlow(_rowIndex.Value).ExecuteAsync(page)
            : await new OpenSongDetailsFlow(_songTitle!).ExecuteAsync(page);

        var managePlaylistsDialog = await songDetails.OpenManagePlaylistsDialogAsync();
        await managePlaylistsDialog.WaitForLoadedAsync();

        await managePlaylistsDialog.SelectPlaylistAsync(_playlistName, "add");

        await managePlaylistsDialog.ApplyAsync();
    }
}
