namespace MyMusic.CLI.Tests.Services;

using System.IO.Abstractions.TestingHelpers;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services;
using Shouldly;
using Xunit;

public class FileScannerTests
{
    [Fact]
    public async Task ScanAsync_SkipsFilesAndFoldersMatchingAnExclusionRule()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["/music/Artist/Album/song.mp3"] = new("a"),
            ["/music/Artist/Album/demo.mp3"] = new("b"),
            ["/music/Podcasts/2024/ep1.mp3"] = new("c"),
            ["/music/.hidden.mp3"] = new("d"),
            ["/music/Artist/Podcasts.mp3"] = new("e"),
        });
        var options = new RepositoryOptions
        {
            Path = "/music",
            ExcludePatterns = ["**/.*", "Podcasts/", "demo.*"],
        };

        var result = await new FileScanner(fileSystem).ScanAsync("/music", options, ct: TestContext.Current.CancellationToken);

        result.Files.Select(f => f.RelativePath).Order().ShouldBe(["Artist/Album/song.mp3", "Artist/Podcasts.mp3"]);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithoutExclusionRules_ReturnsEveryMusicFile()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["/music/Artist/song.mp3"] = new("a"),
            ["/music/.hidden.mp3"] = new("b"),
            ["/music/cover.jpg"] = new("c"),
        });

        var result = await new FileScanner(fileSystem).ScanAsync("/music", new RepositoryOptions { Path = "/music" }, ct: TestContext.Current.CancellationToken);

        result.Files.Select(f => f.RelativePath).Order().ShouldBe([".hidden.mp3", "Artist/song.mp3"]);
    }
}
