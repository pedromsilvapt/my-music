namespace MyMusic.Common.Models;

public record class SongImportMetadata(string SourceFilePath, DateTime CreatedAt, DateTime ModifiedAt, long? SongId = null)
{
    public string SourceFilePath { get; set; } = SourceFilePath;

    public DateTime CreatedAt { get; set; } = CreatedAt;

    public DateTime ModifiedAt { get; set; } = ModifiedAt;

    public long? SongId { get; set; } = SongId;
}