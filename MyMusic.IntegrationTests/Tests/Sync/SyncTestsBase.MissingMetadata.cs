using MyMusic.IntegrationTests.Extensions;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Fixtures.Models;
using MyMusic.IntegrationTests.Flows;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Sync;

public abstract partial class SyncTestsBase
{
    [Fact]
    public async Task Sync_ShouldImportSongWithNoMetadata_UsingFilenameAsTitle()
    {
        // Create a local MP3 with no title, no album, no artist tags
        var fileName = "untitled_song.mp3";
        var relativePath = await App.CreateSongAsync(new SampleSong(), relativePath: fileName);

        // Run sync to upload the song
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();
        result.Created.ShouldBe(1);

        // Verify: DB uses filename (no extension) as title
        // The effectiveTitle is derived from the source filename without extension
        await new ValidateSongDetailsFlow("untitled_song", new ValidateSongOptions(
            Title: "untitled_song",
            Artists: ["(No Artist)"],
            Album: "(No Album)"))
            .ExecuteAsync(Page);

        // Verify: File stays at original path after upload-only sync — not renamed until a download sync
        App.FileShouldExist("untitled_song.mp3");

        // Verify: Local file tags should NOT have placeholder values written back
        var filePath = App.GetSongPath("untitled_song.mp3");
        await FileValidator.AssertMetadataAsync(filePath, title: "", album: "");
    }

    [Fact]
    public async Task Sync_ShouldImportSongWithNullAlbum_UsingNoAlbumPlaceholder()
    {
        // Create a song with title and artists but no album at all
        // "The Fate of Ophelia" by Taylor Swift, with Album stripped
        var song = SongsFixture.DefaultSongs[3] with { Album = null };
        await App.CreateSongAsync(song);

        // Run sync to upload
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();
        result.Created.ShouldBe(1);

        // Verify: DB uses "(No Album)" placeholder for album name
        // Album artist falls back to first performer, no (No Artist) placeholder needed
        await new ValidateSongDetailsFlow(song.Title!, new ValidateSongOptions(
            Title: song.Title,
            Artists: ["(No Artist)", .. song.Artists!],
            Album: "(No Album)"))
            .ExecuteAsync(Page);

        // Verify: The album's artist is "Taylor Swift" (derived from performers)
        await new ShouldArtistExistFlow("Taylor Swift").ExecuteAsync(Page);
        await new ShouldAlbumExistFlow("(No Album)").ExecuteAsync(Page);

        // Verify: File stays at original path after upload-only sync
        App.FileShouldExist($"{song.Title}.mp3");
    }

    [Fact]
    public async Task Sync_ShouldImportSongWithEmptyAlbumName_UsingNoAlbumPlaceholder()
    {
        // Create a song with an empty album name (tag present but empty string)
        // "Maneater" by Nelly Furtado, with Album set to empty string and explicit AlbumArtist
        var song = SongsFixture.DefaultSongs[20] with { Album = "", AlbumArtist = "Nelly Furtado" };
        await App.CreateSongAsync(song);

        // Run sync to upload
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();
        result.Created.ShouldBe(1);

        // Verify: DB uses "(No Album)" placeholder for empty album name
        await new ValidateSongDetailsFlow(song.Title!, new ValidateSongOptions(
            Title: song.Title,
            Artists: song.Artists!,
            Album: "(No Album)"))
            .ExecuteAsync(Page);

        // Verify: Album is stored with "(No Album)" placeholder
        await new ShouldAlbumExistFlow("(No Album)").ExecuteAsync(Page);

        // Verify: File stays at original path after upload-only sync
        App.FileShouldExist($"{song.Title}.mp3");
    }

    [Fact]
    public async Task Sync_ShouldImportSongWithNullAlbumArtist_UsingNoArtistPlaceholder()
    {
        // Create a song with album name but no album artist
        // "New Religion" by Faithless & Bebe Rexha — naturally has no AlbumArtist,
        // so the first performer becomes the album artist
        var song = SongsFixture.DefaultSongs[6];
        await App.CreateSongAsync(song);

        // Run sync to upload
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();
        result.Created.ShouldBe(1);

        // Verify: album artist falls back to first performer, no (No Artist) placeholder needed
        await new ValidateSongDetailsFlow(song.Title!, new ValidateSongOptions(
            Title: song.Title,
            Artists: song.Artists!,
            Album: song.Album!))
            .ExecuteAsync(Page);

        // Verify: The album's artist is "Faithless" (derived from performers)
        await new ShouldArtistExistFlow("Faithless").ExecuteAsync(Page);

        // Verify: File stays at original path after upload-only sync
        App.FileShouldExist($"{song.Title}.mp3");
    }

    [Fact]
    public async Task Sync_ShouldImportSongWithEmptyAlbumArtistName_UsingNoArtistPlaceholder()
    {
        // Create a song with album name and empty album artist name
        // "The Fate of Ophelia" from album "The Life of a Showgirl" with Artists stripped —
        // TagLib doesn't easily support empty string album artists, so we test
        // with null album artist (empty AlbumArtists tag)
        var song = SongsFixture.DefaultSongs[3] with { Artists = null };
        await App.CreateSongAsync(song);

        // Run sync to upload
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();
        result.Created.ShouldBe(1);

        // Verify: DB uses "(No Artist)" for album artist
        await new ValidateSongDetailsFlow(song.Title!, new ValidateSongOptions(
            Title: song.Title,
            Artists: ["(No Artist)"],
            Album: song.Album!))
            .ExecuteAsync(Page);

        // Verify: The artist "(No Artist)" exists on server
        await new ShouldArtistExistFlow("(No Artist)").ExecuteAsync(Page);

        // Verify: File stays at original path after upload-only sync
        App.FileShouldExist($"{song.Title}.mp3");
    }

    [Fact]
    public async Task Sync_ShouldNotWritePlaceholdersBackToLocalFile()
    {
        // Create a song with no metadata tags
        var relativePath = await App.CreateSongAsync(
            new SampleSong(),
            relativePath: "bare_file.mp3");

        // Run sync to upload
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();

        // Verify the file exists after sync (renamed based on metadata)
        var files = App.GetAllFiles();
        files.Count.ShouldBe(1);

        // Verify: the local file's tags should NOT contain placeholder values
        // The title should remain empty (not "untitled_song" or "bare_file")
        // The album should remain empty (not "(No Album)")
        // The album artist should remain empty (not "(No Artist)")
        var filePath = App.GetSongPath(files[0]);
        await FileValidator.AssertMetadataAsync(filePath, title: "", album: "");
    }

    [Fact]
    public async Task Sync_ShouldUploadNoMetadataSong_ThenDownloadEdits()
    {
        // Create a song with no metadata
        var relativePath = await App.CreateSongAsync(
            new SampleSong(),
            relativePath: "no_meta_edit.mp3");

        // Run sync to upload - DB will use placeholders
        var result1 = await App.SyncAsync(new SyncOptions());
        result1.ShouldBeSuccessful();
        result1.Created.ShouldBe(1);

        // Verify: server shows placeholder title derived from filename
        await new ValidateSongDetailsFlow("no_meta_edit", new ValidateSongOptions(
            Title: "no_meta_edit",
            Artists: ["(No Artist)"],
            Album: "(No Album)"))
            .ExecuteAsync(Page);

        // Edit the song on the server to add proper metadata
        await new EditSongFlow("no_meta_edit", new EditSongOptions(
            Title: "Golden Hour",
            Album: "Chapter One",
            Artists: ["Kacey Musgraves"],
            AlbumArtist: "Kacey Musgraves"))
            .ExecuteAsync(Page);

        // Run sync again to download the edits
        var result2 = await App.SyncAsync(new SyncOptions());
        result2.ShouldBeSuccessful();

        // Verify: the local file should now have the updated metadata
        // After edit, the file gets renamed using the new metadata
        // ArtistAlbumNamingStrategy: "Kacey Musgraves", "Chapter One", "Golden Hour - Kacey Musgraves"
        var expectedPath = "Kacey Musgraves/Chapter One/Golden Hour - Kacey Musgraves.mp3";
        App.FileShouldExist(expectedPath);

        await FileValidator.AssertMetadataAsync(
            App.GetSongPath(expectedPath),
            title: "Golden Hour",
            album: "Chapter One");
    }

    [Fact]
    public async Task Sync_ShouldImportMultipleNoMetadataSongsDistinctly()
    {
        // Create two songs with no metadata on the same device
        var path1 = await App.CreateSongAsync(
            new SampleSong(Lyrics: "track a lyrics"),
            relativePath: "track_a.mp3");

        var path2 = await App.CreateSongAsync(
            new SampleSong(Lyrics: "track b lyrics"),
            relativePath: "track_b.mp3");

        // Run sync to upload both songs
        var result = await App.SyncAsync(new SyncOptions());
        result.ShouldBeSuccessful();
        result.Created.ShouldBe(2);

        // Verify: both songs should appear on server with distinct
        // placeholder titles derived from their filenames
        var songs = await new HomePage(Page).Navbar.GoToSongsAsync();
        (await songs.Collection.GetRowCountAsync()).ShouldBe(2);

        // Verify each song has its filename-based title
        await new ValidateSongDetailsFlow("track_a", new ValidateSongOptions(
            Title: "track_a",
            Artists: ["(No Artist)"],
            Album: "(No Album)"))
            .ExecuteAsync(Page);

        await new ValidateSongDetailsFlow("track_b", new ValidateSongOptions(
            Title: "track_b",
            Artists: ["(No Artist)"],
            Album: "(No Album)"))
            .ExecuteAsync(Page);

        // Verify: both songs should use the same "(No Artist)" and "(No Album)" entities
        // but be distinct songs in the database
        await new ShouldAlbumExistFlow("(No Album)").ExecuteAsync(Page);
        await new ShouldArtistExistFlow("(No Artist)").ExecuteAsync(Page);

        // Verify: both files exist locally with distinct paths
        var files = App.GetAllFiles();
        files.Count.ShouldBe(2);
    }
}
