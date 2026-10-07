namespace MyMusic.CLI.Configuration;

public class MyMusicOptions
{
    public ServerOptions Server { get; set; } = new();
    public DeviceOptions Device { get; set; } = new();
    public RepositoryOptions Repository { get; set; } = new();
    public SyncOptions Sync { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();
}

public class ServerOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5000/api";
    public long? UserId { get; set; }
    public string? UserName { get; set; }
}

public class DeviceOptions
{
    public string Name { get; set; } = "My Device";
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? NamingTemplate { get; set; }
    public bool ImportOnPurchase { get; set; } = false;
}

public class RepositoryOptions
{
    public string Path { get; set; } = "";
    public List<string> ExcludePatterns { get; set; } = [];
    public List<string> MusicExtensions { get; set; } = [".mp3"];
}

public class SyncOptions
{
    /// <summary>
    /// Whether the chunk sizes follow the server's response times, between their minimum and maximum.
    /// When false, every request has the configured size.
    /// </summary>
    public bool AdaptiveChunks { get; set; } = true;

    /// <summary>How long a chunked request should take when the chunks are adaptive.</summary>
    public double TargetRequestSeconds { get; set; } = 0.5;

    /// <summary>Files per check request.</summary>
    public ChunkOptions CheckChunk { get; set; } = new() { Size = 50, Min = 10, Max = 1000 };

    /// <summary>Files per conflict resolution request.</summary>
    public ChunkOptions ResolveChunk { get; set; } = new() { Size = 200, Min = 25, Max = 1000 };
}

public class ChunkOptions
{
    /// <summary>The size the sync starts at, and keeps when the chunks are not adaptive.</summary>
    public int Size { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
}

public class LoggingOptions
{
    public bool EnableFileLogging { get; set; }
    public string FilePath { get; set; } = "mymusic-cli.log";
}