using MyMusic.Common.Metadata;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.NamingStrategies;

public interface INamingStrategy
{
    string Generate(SongMetadata song);

    public static string SanitizeFilename(string filename) => FilenameUtils.SanitizeFilename(filename);
}