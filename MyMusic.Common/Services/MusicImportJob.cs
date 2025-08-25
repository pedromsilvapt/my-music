using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MyMusic.Common.Services;

public class MusicImportJob(ILogger logger)
{
    private readonly List<ImportSkipReason> _skipReasons = [];
    
    private readonly Dictionary<string, string> _fileMapping = [];

    public Guid Guid { get; } = Guid.NewGuid();

    public IReadOnlyList<ImportSkipReason> SkipReasons => _skipReasons;
    
    public IReadOnlyDictionary<string, string> FileMapping => _fileMapping;

    public void AddSkipReason(ImportSkipReason skipReason)
    {
        Debug.Assert(skipReason != null, nameof(skipReason) + " != null");

        _skipReasons.Add(skipReason);

        logger.LogError(skipReason.Message, skipReason.MessageArgs);
    }

    public void AddFileMapping(string sourceFilePath, string targetFilePath)
    {
        if (!_fileMapping.TryAdd(sourceFilePath, targetFilePath))
        {
            logger.LogError("Failed to add mapping for {SourceFilePath} => {TargetFilePath}, key already exists in dictionary.", sourceFilePath, targetFilePath);
        }
    }
}

public abstract class ImportSkipReason(string sourceFilePath)
{
    public string SourceFilePath => sourceFilePath;

    public abstract string Message { get; }

    public virtual object[] MessageArgs => [sourceFilePath];
}

public class MissingTitleSkipReason(string sourceFilePath) : ImportSkipReason(sourceFilePath)
{
    public override string Message => "Cannot import song, song metadata does not have a title: {File}.";
}

public class MissingAlbumSkipReason(string sourceFilePath) : ImportSkipReason(sourceFilePath)
{
    public override string Message => "Cannot import song, song metadata does not have an album: {File}.";
}

public class MissingAlbumNameSkipReason(string sourceFilePath) : ImportSkipReason(sourceFilePath)
{
    public override string Message => "Cannot import song, album metadata does not have a name: {File}.";
}

public class MissingAlbumArtistSkipReason(string sourceFilePath) : ImportSkipReason(sourceFilePath)
{
    public override string Message => "Cannot import song, album metadata does not have an artist: {File}.";
}

public class MissingAlbumArtistNameSkipReason(string sourceFilePath) : ImportSkipReason(sourceFilePath)
{
    public override string Message => "Cannot import song, album's artist metadata does not have a name: {File}.";
}

public class DuplicateChecksumSkipReason(
    string sourceFilePath,
    string fullLabel,
    string checksum,
    string checksumAlgorithmName,
    string existingLabel,
    long existingId) : ImportSkipReason(sourceFilePath)
{
    public override string Message =>
        "Cannot import song, checksum is duplicate: {ImportedSong} with checksum {Checksum} {ChecksumAlgo} matches existing song {ExistingSong} {ExistingSongId}: {File}";

    public override object[] MessageArgs =>
        [fullLabel, checksum, checksumAlgorithmName, existingLabel, existingId, SourceFilePath];
}

public class DuplicateFilePathSkipReason(
    string sourceFilePath,
    string fullLabel,
    string targetFilePath,
    string existingLabel,
    long existingId) : ImportSkipReason(sourceFilePath)
{
    public override string Message =>
        "Cannot import song, target file path when importing: {ImportedSong} with file path {FilePath} matches existing song {ExistingSong} {ExistingSongId}: {File}";

    public override object[] MessageArgs => [fullLabel, targetFilePath, existingLabel, existingId, SourceFilePath];
}
