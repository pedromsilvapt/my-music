namespace MyMusic.IntegrationTests.Fixtures;

public static class FileValidator
{
    public static Task<FileMetadata> GetMetadataAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new Exception($"File does not exist: {filePath}");
        }

        using var tfile = TagLib.File.Create(filePath);
        var tag = tfile.Tag;

        return Task.FromResult(new FileMetadata(
            Title: tag.Title ?? "",
            Album: tag.Album ?? "",
            Artists: tag.Performers ?? [],
            Genres: tag.Genres ?? [],
            Year: tag.Year > 0 ? (int)tag.Year : null,
            Track: tag.Track > 0 ? (int)tag.Track : null,
            Duration: tfile.Properties.Duration));
    }

    public static Task AssertFileExistsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new Exception($"File does not exist: {filePath}");
        }
        return Task.CompletedTask;
    }

    public static async Task AssertMetadataAsync(
        string filePath,
        string? title = null,
        string? album = null,
        string[]? artists = null,
        string[]? genres = null,
        int? year = null)
    {
        var metadata = await GetMetadataAsync(filePath);

        if (title != null && metadata.Title != title)
        {
            throw new Exception($"Expected title '{title}' but was '{metadata.Title}'");
        }

        if (album != null && metadata.Album != album)
        {
            throw new Exception($"Expected album '{album}' but was '{metadata.Album}'");
        }

        if (artists != null && !metadata.Artists.SequenceEqual(artists))
        {
            throw new Exception($"Expected artists [{string.Join(", ", artists)}] but was [{string.Join(", ", metadata.Artists)}]");
        }

        if (genres != null && !metadata.Genres.SequenceEqual(genres))
        {
            throw new Exception($"Expected genres [{string.Join(", ", genres)}] but was [{string.Join(", ", metadata.Genres)}]");
        }

        if (year != null && metadata.Year != year)
        {
            throw new Exception($"Expected year {year} but was {metadata.Year}");
        }
    }
}
