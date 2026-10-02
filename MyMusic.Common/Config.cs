namespace MyMusic.Common;

public class Config
{
    public required string MusicRepositoryPath { get; set; }

    public string DefaultNamingTemplate { get; set; } =
        "{{ album.artist.name ?? artists[0].name ?? \"Unknown\" }}/{{ album.name ?? \"No Album\" }}/{{ simple_label }}{{ extension ?? \".mp3\" }}";

    public string? SeedPath { get; set; }

    public int WishlistCheckIntervalMinutes { get; set; } = 60;

    public int WishlistMaxResultsToHash { get; set; } = 50;

    public bool BitrateBackfillEnabled { get; set; }

    public bool SongHistoryWorkerEnabled { get; set; } = true;

    public int SongHistoryWorkerIntervalSeconds { get; set; } = 10;

    public int SongHistoryWorkerBatchSize { get; set; } = 50;

    /// <summary>
    ///     How many songs without a <c>created</c> history baseline get one recorded per song history worker cycle.
    /// </summary>
    public int SongHistoryBaselineBatchSize { get; set; } = 50;

    /// <summary>
    ///     How many songs get their play count only history revisions removed per song history worker cycle.
    /// </summary>
    public int SongHistoryPlayCountCleanupBatchSize { get; set; } = 50;

    /// <summary>
    ///     How many songs a single user can import at the same time, across all requests (uploads, sync, purchases...).
    /// </summary>
    public int MaxConcurrentImportsPerUser { get; set; } = 16;
}