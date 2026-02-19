namespace MyMusic.Common.Utilities;

public static class FilenameUtils
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static string SanitizeFilename(string filename)
    {
        return string.Join("_", filename.Split(InvalidFileNameChars, StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd('.');
    }
}