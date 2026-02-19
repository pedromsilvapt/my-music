using System.IO.Abstractions;
using System.Text.RegularExpressions;
using MyMusic.CLI.Configuration;

namespace MyMusic.CLI.Services;

public interface IFileScanner
{
    Task<List<FileMetadata>> ScanAsync(string rootPath, RepositoryOptions options, CancellationToken ct = default);
}

public record FileMetadata(string RelativePath, string FullPath, DateTime ModifiedAt, DateTime CreatedAt);

public class FileScanner(IFileSystem fileSystem) : IFileScanner
{
    public Task<List<FileMetadata>> ScanAsync(string rootPath, RepositoryOptions options,
        CancellationToken ct = default)
    {
        var files = new List<FileMetadata>();

        if (!fileSystem.Directory.Exists(rootPath))
        {
            return Task.FromResult(files);
        }

        var allFiles = fileSystem.Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);

        foreach (var filePath in allFiles)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');

            if (ShouldExclude(relativePath, options.ExcludePatterns))
            {
                continue;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (!options.MusicExtensions.Contains(extension))
            {
                continue;
            }

            var fileInfo = fileSystem.FileInfo.New(filePath);
            files.Add(new FileMetadata(relativePath, filePath.Replace('\\', '/'), fileInfo.LastWriteTimeUtc,
                fileInfo.CreationTimeUtc));
        }

        return Task.FromResult(files);
    }

    private static bool ShouldExclude(string relativePath, List<string> excludePatterns)
    {
        foreach (var pattern in excludePatterns)
        {
            var regexPattern = GlobToRegex(pattern);
            if (Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GlobToRegex(string glob)
    {
        var regex = "^";
        foreach (var c in glob)
        {
            regex += c switch
            {
                '*' => ".*",
                '?' => ".",
                '.' => "\\.",
                '/' => "[\\\\/]",
                '\\' => "[\\\\/]",
                '[' => "[",
                ']' => "]",
                _ => Regex.Escape(c.ToString()),
            };
        }

        regex += "$";
        return regex;
    }
}