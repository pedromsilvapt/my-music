using System.Text;
using MyMusic.Common.Services;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

/// <summary>
/// Pins the checksum output for fixed inputs. Sync clients compute the same checksums on their own
/// (CLI, mobile), so these values are the shared test vectors: the client tests assert the very
/// same strings.
/// </summary>
public class ChecksumServiceSpecs
{
    public static TheoryData<string, byte[], string> Vectors => new()
    {
        { "empty", [], "maoG0wFHmNhgAcMkRo1Jfw==" },
        { "abc", Encoding.ASCII.GetBytes("abc"), "BrBatnM6YYV4r1+UiS85UA==" },
        { "1 MiB of i % 251", Enumerable.Range(0, 1024 * 1024).Select(i => (byte)(i % 251)).ToArray(), "U3ONmAmMq7puDXrDa4wQ/w==" },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ComputeChecksumFromBytes_XxHash128_MatchesSharedVectors(string name, byte[] content, string expected)
    {
        ChecksumService.ComputeChecksumFromBytes(content, "XxHash128").ShouldBe(expected, name);
    }
}
