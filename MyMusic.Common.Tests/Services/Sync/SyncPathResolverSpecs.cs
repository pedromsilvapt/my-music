using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncPathResolverSpecs
{
    private readonly SyncPathResolver _resolver = new();

    private static readonly string DefaultTemplate = "{{ album.artist.name ?? artists[0].name ?? \"Unknown\" }}/{{ album.name ?? \"No Album\" }}/{{ simple_label }}{{ extension ?? \".mp3\" }}";

    private static TemplateNamingStrategy Strategy(string? template = null) =>
        new(template ?? DefaultTemplate);

    #region GetUniquePath

    [Fact]
    public void GetUniquePath_UnusedBasePath_ReturnsBasePath()
    {
        _resolver.GetUniquePath("/music/song.mp3", new HashSet<string>()).ShouldBe("/music/song.mp3");
    }

    [Fact]
    public void GetUniquePath_Collision_ReturnsSuffixedPath()
    {
        var used = new HashSet<string> { "/music/song.mp3" };

        _resolver.GetUniquePath("/music/song.mp3", used).ShouldBe("/music/song (2).mp3");
    }

    [Fact]
    public void GetUniquePath_MultipleCollisions_IncrementsSuffix()
    {
        var used = new HashSet<string>
        {
            "/music/song.mp3",
            "/music/song (2).mp3",
            "/music/song (3).mp3",
        };

        _resolver.GetUniquePath("/music/song.mp3", used).ShouldBe("/music/song (4).mp3");
    }

    [Fact]
    public void GetUniquePath_DoesNotMutateExistingPaths()
    {
        var used = new HashSet<string> { "/music/song.mp3" };
        var snapshot = used.ToHashSet();

        _resolver.GetUniquePath("/music/song.mp3", used);

        used.ShouldBe(snapshot, "GetUniquePath must not mutate the existingPaths set");
    }

    [Fact]
    public void GetUniquePath_NoDirectory_WorksForBareFileName()
    {
        _resolver.GetUniquePath("song.mp3", new HashSet<string> { "song.mp3" }).ShouldBe("song (2).mp3");
    }

    #endregion

    #region ComputePendingActionPath

    [Fact]
    public void ComputePendingActionPath_SongNull_ReturnsDevicePath_NoRename()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var sd = scenario.CreateSongDevice(device, song: null, "/music/keep.mp3");

        var (path, previousPath) = _resolver.ComputePendingActionPath(sd, Strategy(), new HashSet<string>());

        path.ShouldBe("/music/keep.mp3");
        previousPath.ShouldBeNull();
    }

    [Fact]
    public void ComputePendingActionPath_GeneratedEqualsDevicePath_NoRename()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        // Build the SongDevice path from the same template the strategy will regenerate so the
        // generated path equals the existing path (no rename expected).
        var strategy = Strategy("/music/{{ album.artist.name }}/{{ album.name }}/{{ simple_label }}{{ extension }}");
        var metadata = EntityConverter.ToSong(song);
        var generated = strategy.Generate(metadata);
        var sd = scenario.CreateSongDevice(device, song, generated);

        var (path, previousPath) = _resolver.ComputePendingActionPath(sd, strategy, new HashSet<string> { sd.DevicePath });

        path.ShouldBe(sd.DevicePath);
        previousPath.ShouldBeNull();
    }

    [Fact]
    public void ComputePendingActionPath_GeneratedDiffersFromDevicePath_ReturnsRename()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var sd = scenario.CreateSongDevice(device, song, "/music/old/path/song.mp3");
        var strategy = Strategy("/music/{{ album.artist.name }}/{{ album.name }}/{{ simple_label }}{{ extension }}");

        var (path, previousPath) = _resolver.ComputePendingActionPath(sd, strategy, new HashSet<string> { sd.DevicePath });

        path.ShouldNotBe(sd.DevicePath);
        previousPath.ShouldBe(sd.DevicePath);
    }

    [Fact]
    public void ComputePendingActionPath_GeneratedPathCollidesWithUsedPaths_ReturnsUniqueVariant()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var sd = scenario.CreateSongDevice(device, song, "/music/old/path/song.mp3");
        var strategy = Strategy("/music/{{ album.artist.name }}/{{ album.name }}/{{ simple_label }}{{ extension }}");

        // Compute the base path the strategy would generate, then seed usedPaths with both the
        // old device path and that base path so the resolver must produce a " (2)" variant.
        var metadata = EntityConverter.ToSong(song);
        var expectedBase = strategy.Generate(metadata);
        var used = new HashSet<string> { sd.DevicePath, expectedBase };

        var (path, previousPath) = _resolver.ComputePendingActionPath(sd, strategy, used);

        // The generated base path collided, so the resolver must return a unique variant that is
        // neither the old device path nor the colliding base path, while signalling a rename.
        path.ShouldNotBe(sd.DevicePath);
        path.ShouldNotBe(expectedBase);
        previousPath.ShouldBe(sd.DevicePath);
    }

    [Fact]
    public void ComputePendingActionPath_DoesNotMutateUsedPaths()
    {
        var scenario = new Scenario();
        var device = scenario.CreateDevice();
        var song = scenario.CreateSong("Song");
        var sd = scenario.CreateSongDevice(device, song, "/music/old/path/song.mp3");
        var strategy = Strategy("/music/{{ album.artist.name }}/{{ album.name }}/{{ simple_label }}{{ extension }}");
        var used = new HashSet<string> { sd.DevicePath };
        var snapshot = used.ToHashSet();

        _resolver.ComputePendingActionPath(sd, strategy, used);

        used.ShouldBe(snapshot, "ComputePendingActionPath must not mutate usedPaths (callers add the result)");
    }

    #endregion
}