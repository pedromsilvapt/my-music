using System.IO.Hashing;
using System.IO.Abstractions;

namespace MyMusic.Common.Services;

public static class ChecksumService
{
    public static NonCryptographicHashAlgorithm CreateChecksumAlgorithm() => new XxHash128();

    public static NonCryptographicHashAlgorithm CreateChecksumAlgorithmByName(string name) => name switch
    {
        "XxHash128" => new XxHash128(),
        _ => throw new ArgumentException($"Unknown checksum algorithm: {name}", nameof(name)),
    };

    public static string CalculateChecksum(IFileSystem fs, NonCryptographicHashAlgorithm algorithm, string filePath)
    {
        using var file = fs.File.OpenRead(filePath);

        return CalculateChecksum(algorithm, file);
    }

    public static string CalculateChecksum(NonCryptographicHashAlgorithm algorithm, byte[] bytes)
    {
        using var memory = new MemoryStream(bytes);

        return CalculateChecksum(algorithm, memory);
    }

    public static string CalculateChecksum(NonCryptographicHashAlgorithm algorithm, Stream stream)
    {
        algorithm.Append(stream);

        var hash = algorithm.GetHashAndReset();

        return Convert.ToBase64String(hash);
    }

    public static string ComputeChecksumFromBytes(byte[] fileBytes, string checksumAlgorithmName)
    {
        return CalculateChecksum(CreateChecksumAlgorithmByName(checksumAlgorithmName), fileBytes);
    }
}
