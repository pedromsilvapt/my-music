namespace MyMusic.CLI.Tests.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

internal static class ChunkTunings
{
    /// <summary>Chunks that keep their size for the whole sync, whatever the requests take.</summary>
    public static SyncChunkTuning Fixed(int checkSize = 50, int resolveSize = 200) => new()
    {
        Adaptive = false,
        Check = new ChunkSizeRange(checkSize, checkSize, checkSize),
        Resolve = new ChunkSizeRange(resolveSize, resolveSize, resolveSize)
    };

    /// <summary>Chunks that follow the response times, starting at the given sizes.</summary>
    public static SyncChunkTuning Adaptive(ChunkSizeRange check, ChunkSizeRange? resolve = null) => new()
    {
        Adaptive = true,
        Check = check,
        Resolve = resolve ?? new ChunkSizeRange(200, 25, 1000)
    };
}
