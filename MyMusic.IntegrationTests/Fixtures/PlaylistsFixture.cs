using Microsoft.Playwright;
using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class PlaylistsFixture
{
    public static string[] DefaultPlaylists { get; } =
    [
        "Test Playlist 1",
        "Test Playlist 2",
        "Test Playlist 3",
    ];

    public async Task<List<PlaylistData>> SeedAsync(IAPIRequestContext api, long userId, string[]? playlists = null)
    {
        var samplePlaylists = playlists ?? DefaultPlaylists;
        var data = new List<PlaylistData>();

        foreach (var playlistName in samplePlaylists)
        {
            var response = await api.PostWithTraceAsync("/api/playlists", new()
            {
                DataObject = new
                {
                    name = playlistName,
                },
            });

            response.Ok.ShouldBeTrue($"Failed to create playlist: {response.Status} {response.StatusText}");

            var json = await response.JsonAsync();
            var id = json?.GetProperty("playlist").GetProperty("id").GetInt64()
                ?? throw new InvalidOperationException("Failed to get playlist ID from response");
            var name = json?.GetProperty("playlist").GetProperty("name").GetString()
                ?? playlistName;

            data.Add(new PlaylistData(id, name));
        }

        return data;
    }

    public async Task<PlaylistData> SeedAsync(IAPIRequestContext api, long userId, string playlist)
    {
        var playlists = await SeedAsync(api, userId, [playlist]);
        return playlists[0];
    }

    /// <summary>
    /// Creates a playlist named <paramref name="playlist"/> containing the given songs.
    /// </summary>
    public async Task<PlaylistData> SeedWithSongsAsync(IAPIRequestContext api, long userId, string playlist,
        params SongData[] songs)
    {
        var data = await SeedAsync(api, userId, playlist);
        await AddSongsAsync(api, data.Id, songs);
        return data;
    }

    /// <summary>
    /// Creates a playlist named <paramref name="playlist"/> containing the given songs, and shares it
    /// with <paramref name="recipientId"/>. Must be called with the owner's request context.
    /// </summary>
    public async Task<PlaylistData> SeedSharedAsync(IAPIRequestContext api, long userId, string playlist,
        long recipientId, params SongData[] songs)
    {
        var data = await SeedWithSongsAsync(api, userId, playlist, songs);
        await ShareAsync(api, data.Id, recipientId);
        return data;
    }

    /// <summary>
    /// Shares (or revokes the share of) an existing playlist with <paramref name="recipientId"/>.
    /// Must be called with the owner's request context.
    /// </summary>
    public async Task ShareAsync(IAPIRequestContext api, long playlistId, long recipientId,
        ShareAction action = ShareAction.Add)
    {
        var response = await api.PostWithTraceAsync("/api/playlists/manage-shares", new()
        {
            DataObject = new
            {
                playlistIds = new[] { playlistId },
                shares = new[] { new { userId = recipientId, action = action.ToString() } },
            },
        });

        response.Ok.ShouldBeTrue($"Failed to {action} playlist share: {response.Status} {response.StatusText}");
    }

    /// <summary>
    /// Adds the given songs to an existing playlist.
    /// </summary>
    public async Task AddSongsAsync(IAPIRequestContext api, long playlistId, params SongData[] songs)
    {
        var response = await api.PostWithTraceAsync($"/api/playlists/{playlistId}/songs", new()
        {
            DataObject = new
            {
                songIds = songs.Select(s => s.Id).ToArray(),
            },
        });

        response.Ok.ShouldBeTrue($"Failed to add songs to playlist: {response.Status} {response.StatusText}");
    }
}
