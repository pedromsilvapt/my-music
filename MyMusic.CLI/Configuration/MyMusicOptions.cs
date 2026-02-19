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
}

public class RepositoryOptions
{
    public string Path { get; set; } = "";
    public List<string> ExcludePatterns { get; set; } = [];
    public List<string> MusicExtensions { get; set; } = [".mp3"];
}

public class SyncOptions
{
    public int ChunkSize { get; set; } = 50;
}

public class LoggingOptions
{
    public bool EnableFileLogging { get; set; }
    public string FilePath { get; set; } = "mymusic-cli.log";
}