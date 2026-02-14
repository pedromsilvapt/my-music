namespace MyMusic.Common.Sources;

public record SourcePurchasedSong
{
    public bool Success { get; set; }

    public bool Error { get; set; }

    public string? ErrorMessage { get; set; }

    public int Progress { get; set; }
}