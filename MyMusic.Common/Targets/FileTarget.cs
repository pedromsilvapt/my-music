using System.IO.Abstractions;
using Microsoft.VisualBasic;
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

    public FileTarget(IFileSystem fileSystem) : this(new ArtistAlbumNamingStrategy(), fileSystem) { }

    public async Task Save(Stream data, SongMetadata? metadata = null, CancellationToken cancellationToken = default)
    {
        EnsureFilePath(metadata);

        Metadata = metadata;

        FileSystem.Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        
        await using (var fs = FileSystem.FileStream.New(FilePath!, FileMode.OpenOrCreate))
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

        return FileSystem.FileStream.New(FilePath, FileMode.Open);
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

        await TagConverter.FromSong(metadata, tfile.Tag, cancellationToken: cancellationToken);

        tfile.Save();
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

    public async Task Relocate(CancellationToken cancellationToken = default)
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

        var newFilePath = FileSystem.Path.Combine(Folder, NamingStrategy.Generate(Metadata));

        // If the path is indeed different, move the file
        if (newFilePath != FilePath)
        {
            FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(newFilePath)!);

            FileSystem.File.Move(FilePath, newFilePath);

            FilePath = newFilePath;
        }
    }

    public void EnsureFilePath(SongMetadata? metadata)
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

            FilePath = FileSystem.Path.Combine(Folder, NamingStrategy.Generate(metadata));
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