namespace MyMusic.Common.Models;

public record class SongImportMetadata(string SourceFilePath, DateTime CreatedAt, DateTime ModifiedAt)
{
    public string SourceFilePath { get; set; } = SourceFilePath;

    public DateTime CreatedAt { get; set; } = CreatedAt;

    public DateTime ModifiedAt { get; set; } = ModifiedAt;
}