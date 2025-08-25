using MyMusic.Common.Metadata;

namespace MyMusic.Common.NamingStrategies;

public interface INamingStrategy
{
    // TODO Add song extension here
    string Generate(SongMetadata song);

    public string SanitizeFilename(string filename)
    {
        var invalids = Path.GetInvalidFileNameChars();

        return string.Join("_", filename.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }
}