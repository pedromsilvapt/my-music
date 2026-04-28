using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Fixtures;

public class Fixtures
{
    public DevicesFixture Devices { get; } = new();
    public PlaylistsFixture Playlists { get; } = new();
    public SongsFixture Songs { get; } = new();
    public ArtistsFixture Artists { get; } = new();
    public AlbumsFixture Albums { get; } = new();
    public GenresFixture Genres { get; } = new();
    public List<ArtistData> ArtistsData { get; private set; } = [];
    public List<AlbumData> AlbumsData { get; private set; } = [];
    public List<DeviceData> DevicesData { get; private set; } = [];
    public List<GenreData> GenresData { get; private set; } = [];
    public List<PlaylistData> PlaylistsData { get; private set; } = [];
    public List<SongData> SongsData { get; private set; } = [];

    public async Task SeedAsync(IAPIRequestContext api, long userId)
    {
        ArtistsData = await Artists.SeedAsync(api, userId);
        GenresData = await Genres.SeedAsync(api, userId);
        AlbumsData = await Albums.SeedAsync(api, userId, ArtistsData);
        DevicesData = await Devices.SeedAsync(api, userId);
        PlaylistsData = await Playlists.SeedAsync(api, userId);
        SongsData = await Songs.SeedAsync(api, userId);
    }

    public int TotalEntities =>
        DevicesData.Count +
        PlaylistsData.Count +
        SongsData.Count +
        ArtistsData.Count +
        AlbumsData.Count +
        GenresData.Count;
}
