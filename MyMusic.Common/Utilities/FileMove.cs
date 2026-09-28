using System.IO.Abstractions;

namespace MyMusic.Common.Utilities;

/// <summary>
///     A song file moving to another path in the repository, which can be undone if the transaction claiming that path
///     fails to commit.
/// </summary>
public sealed record FileMove(string From, string To)
{
    /// <summary>Moves the file to <see cref="To"/>, creating its folder if needed. Never overwrites another file.</summary>
    public void Apply(IFileSystem fileSystem)
    {
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(To)!);
        fileSystem.File.Move(From, To);
    }

    /// <summary>Moves the file back to <see cref="From"/>, if it is still at <see cref="To"/>.</summary>
    public void Undo(IFileSystem fileSystem)
    {
        if (fileSystem.File.Exists(To))
        {
            fileSystem.File.Move(To, From);
        }
    }
}
