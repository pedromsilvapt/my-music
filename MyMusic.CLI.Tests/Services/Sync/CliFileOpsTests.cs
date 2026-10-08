namespace MyMusic.CLI.Tests.Services.Sync;

using System.IO.Abstractions.TestingHelpers;
using System.Text;
using MyMusic.CLI.Services.Sync;
using Shouldly;
using Xunit;

public class CliFileOpsTests
{
    /// <summary>
    /// The test vectors shared with the server (<c>ChecksumServiceSpecs</c>) and the mobile app: the
    /// server compares these checksums with its own, so every client must produce the same strings.
    /// </summary>
    public static TheoryData<string, byte[], string> Vectors => new()
    {
        { "empty", [], "maoG0wFHmNhgAcMkRo1Jfw==" },
        { "abc", Encoding.ASCII.GetBytes("abc"), "BrBatnM6YYV4r1+UiS85UA==" },
        { "1 MiB of i % 251", Enumerable.Range(0, 1024 * 1024).Select(i => (byte)(i % 251)).ToArray(), "U3ONmAmMq7puDXrDa4wQ/w==" },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public async Task ComputeChecksumAsync_XxHash128_MatchesSharedVectors(string name, byte[] content, string expected)
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/music/song.mp3"] = new(content) });
        var fileOps = new CliFileOps(fileSystem);

        var checksum = await fileOps.ComputeChecksumAsync("/music/song.mp3", "XxHash128", TestContext.Current.CancellationToken);

        checksum.ShouldBe(expected, name);
    }

    [Fact]
    public async Task ComputeChecksumAsync_UnknownAlgorithm_Throws()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/music/song.mp3"] = new("abc") });
        var fileOps = new CliFileOps(fileSystem);

        await Should.ThrowAsync<NotSupportedException>(() => fileOps.ComputeChecksumAsync("/music/song.mp3", "Sha256", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetModificationTimeAsync_ChangesTheFileModifiedDate()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { ["/music/song.mp3"] = new("abc") });
        var fileOps = new CliFileOps(fileSystem);
        var date = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        await fileOps.SetModificationTimeAsync("/music/song.mp3", date, TestContext.Current.CancellationToken);

        (await fileOps.GetModificationTimeAsync("/music/song.mp3", TestContext.Current.CancellationToken)).ShouldBe(date);
    }
}
