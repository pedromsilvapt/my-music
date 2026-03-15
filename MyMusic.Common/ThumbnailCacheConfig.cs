namespace MyMusic.Common;

public class ThumbnailCacheConfig
{
    public long MaxCacheSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    public long MaxEntrySizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB

    public int EntryTtlMinutes { get; set; } = 60;

    public string ProxyPathPrefix { get; set; } = "/api/sources/thumbnails/";
}