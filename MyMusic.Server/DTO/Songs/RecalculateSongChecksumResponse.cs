namespace MyMusic.Server.DTO.Songs;

public record RecalculateSongChecksumResponse
{
    /// <summary>Whether the file's checksum differed from the recorded one, which was then updated.</summary>
    public required bool Changed { get; init; }
}
