using System.IO.Abstractions;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;

namespace MyMusic.Common.Targets;

public class FileTarget(INamingStrategy namingStrategy, IFileSystem fileSystem) : ITarget
{
    public string? FilePath { get; set; }

    public string? Folder { get; set; }

    public INamingStrategy NamingStrategy { get; } = namingStrategy;

    public IFileSystem FileSystem { get; } = fileSystem;

    protected SongMetadata? Metadata { get; set; }

    protected NamingMetadata? Naming { get; set; }

    public FileTarget(IFileSystem fileSystem) : this(new ArtistAlbumNamingStrategy(), fileSystem) { }

    public async Task Save(Stream data, SongMetadata? metadata = null, NamingMetadata? naming = null, CancellationToken cancellationToken = default)
    {
        EnsureFilePath(metadata, naming);

        Metadata = metadata;
        Naming = naming;

        FileSystem.Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        await using (var fs = FileSystem.FileStream.New(FilePath!, FileMode.OpenOrCreate, FileAccess.Write))
        {
            await data.CopyToAsync(fs, cancellationToken);
        }

        if (metadata != null)
        {
            await SaveMetadata(metadata, cancellationToken);
        }
    }

    public Stream Read()
    {
        if (FilePath == null)
        {
            throw new Exception("Cannot read from target because FilePath is null.");
        }

        return FileSystem.FileStream.New(FilePath, FileMode.Open, FileAccess.Read);
    }

    public async Task SaveMetadata(SongMetadata metadata, CancellationToken cancellationToken = default)
    {
        if (FilePath == null)
        {
            throw new Exception("Cannot save metadata into target without a file path");
        }

        Metadata = metadata;

        var fileInfo = new FileSystemFileAbstraction(FileSystem.FileInfo.New(FilePath));
        using var tfile = TagLib.File.Create(fileInfo);

        // Write new metadata to the existing tags
        await TagConverter.FromSong(metadata, tfile.Tag, cancellationToken: cancellationToken);

        // Rebuild tags fresh to eliminate old padding and ensure correct sizing
        RebuildTags(tfile);

        tfile.Save();
    }

    private static void RebuildTags(TagLib.File file)
    {
        // Get the underlying tags (Id3v2, Ape, Id3v1, etc.)
        var tags = (file.Tag as TagLib.CombinedTag)?.Tags ?? new[] { file.Tag };

        // Save references to each tag BEFORE removing
        var tagSnapshots = tags.Select(t => new { Type = t.TagTypes, Tag = t }).ToList();

        // Remove all tags from the file
        file.RemoveTags(TagLib.TagTypes.AllTags);

        // Recreate each tag fresh and copy data
        foreach (var snapshot in tagSnapshots)
        {
            var newTag = file.GetTag(snapshot.Type, true);
            snapshot.Tag.CopyTo(newTag, true);
        }
    }

    public Task<SongMetadata> ReadMetadata(CancellationToken cancellationToken = default)
    {
        if (FilePath == null)
        {
            throw new Exception("Cannot read metadata from target without a file path");
        }

        var fileInfo = new FileSystemFileAbstraction(FileSystem.FileInfo.New(FilePath));

        using var tfile = TagLib.File.Create(fileInfo);

        var metadata = TagConverter.ToSong(tfile.Tag, tfile.Properties);

        Metadata = metadata;

        return Task.FromResult(metadata);
    }

    public async Task SetTimestamps(DateTime createdAt, DateTime modifiedAt,
        CancellationToken cancellationToken = default)
    {
        EnsureFilePath(null);

        FileSystem.File.SetCreationTimeUtc(FilePath!, createdAt.ToUniversalTime());
        FileSystem.File.SetLastWriteTimeUtc(FilePath!, modifiedAt.ToUniversalTime());

        await Task.CompletedTask;
    }

    public async Task Relocate(NamingMetadata? naming = null, CancellationToken cancellationToken = default)
    {
        if (FilePath is null)
        {
            throw new Exception("Cannot relocate file if the original FilePath is null.");
        }

        if (Folder is null)
        {
            throw new Exception("Cannot relocate file if the Root Folder Path is null.");
        }

        if (Metadata is null)
        {
            await ReadMetadata(cancellationToken);
        }

        if (Metadata is null || Metadata.Title is null)
        {
            throw new Exception("Cannot relocate file if the metadata title is null.");
        }

        var effectiveNaming = naming ?? Naming ?? NamingMetadata.FromPath(FilePath);
        var newFilePath = FileSystem.Path.Combine(Folder, NamingStrategy.Generate(Metadata, effectiveNaming));

        // If the path is indeed different, move the file
        if (newFilePath != FilePath)
        {
            FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(newFilePath)!);

            FileSystem.File.Move(FilePath, newFilePath);

            FilePath = newFilePath;
        }
    }

    public void EnsureFilePath(SongMetadata? metadata, NamingMetadata? naming = null)
    {
        if (FilePath is null)
        {
            if (metadata is null)
            {
                throw new Exception("Cannot save new file without FilePath because no metadata was provided.");
            }

            if (Folder is null)
            {
                throw new Exception("Cannot save new file without FilePath because no folder was provided.");
            }

            FilePath = FileSystem.Path.Combine(Folder, NamingStrategy.Generate(metadata, naming));
        }
    }
}

public class FileSystemFileAbstraction(IFileInfo fileInfo) : TagLib.File.IFileAbstraction
{
    public string Name => fileInfo.FullName;

    public Stream ReadStream => fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

    public Stream WriteStream => fileInfo.Open(FileMode.Open, FileAccess.ReadWrite);

    public void CloseStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.Close();
    }
}
