namespace MyMusic.Common;

public class Config
{
    public required string MusicRepositoryPath { get; set; }

    public string DefaultNamingTemplate { get; set; } =
        "{{ album.artist.name ?? artists[0].name ?? \"Unknown\" }}/{{ album.name ?? \"No Album\" }}/{{ simple_label }}.mp3";

    public string? SeedPath { get; set; }

    public int WishlistCheckIntervalMinutes { get; set; } = 60;

    public int WishlistMaxResultsToHash { get; set; } = 50;

    public bool BitrateBackfillEnabled { get; set; }
}