namespace MyMusic.Common.NamingStrategies;

public record NamingMetadata
{
    public string Extension { get; init; } = "";
    public string? OriginalFolder { get; init; }
    public string? OriginalName { get; init; }

    public static NamingMetadata FromPath(string? path) => path is null
        ? new NamingMetadata()
        : new NamingMetadata
        {
            Extension = ExtractExtension(path),
            OriginalFolder = ExtractFolder(path),
            OriginalName = ExtractName(path)
        };

    private static string ExtractExtension(string path)
    {
        var lastDot = path.LastIndexOf('.');
        return lastDot >= 0 ? path[lastDot..] : "";
    }

    private static string ExtractFolder(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 ? path[..lastSlash] : "";
    }

    private static string ExtractName(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        var lastDot = path.LastIndexOf('.');
        
        var start = lastSlash >= 0 ? lastSlash + 1 : 0;
        var end = lastDot > lastSlash ? lastDot : path.Length;
        
        return path[start..end];
    }
}
