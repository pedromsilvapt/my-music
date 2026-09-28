using System.IO.Abstractions.TestingHelpers;
using MyMusic.Common.Utilities;
using Shouldly;

namespace MyMusic.Common.Tests.Utilities;

public class FileMoveSpecs
{
    [Fact]
    public void Apply_MovesFileIntoMissingFolder()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/repo/Old/Song.mp3", new MockFileData("content"));
        var move = new FileMove("/repo/Old/Song.mp3", "/repo/New/Song.mp3");

        move.Apply(fileSystem);

        fileSystem.File.Exists("/repo/Old/Song.mp3").ShouldBeFalse();
        fileSystem.File.ReadAllText("/repo/New/Song.mp3").ShouldBe("content");
    }

    [Fact]
    public void Apply_TargetExists_Throws()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/repo/Old/Song.mp3", new MockFileData("content"));
        fileSystem.AddFile("/repo/New/Song.mp3", new MockFileData("other"));
        var move = new FileMove("/repo/Old/Song.mp3", "/repo/New/Song.mp3");

        Should.Throw<IOException>(() => move.Apply(fileSystem));

        fileSystem.File.ReadAllText("/repo/New/Song.mp3").ShouldBe("other");
    }

    [Fact]
    public void Undo_MovesFileBack()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("/repo/Old/Song.mp3", new MockFileData("content"));
        var move = new FileMove("/repo/Old/Song.mp3", "/repo/New/Song.mp3");
        move.Apply(fileSystem);

        move.Undo(fileSystem);

        fileSystem.File.ReadAllText("/repo/Old/Song.mp3").ShouldBe("content");
        fileSystem.File.Exists("/repo/New/Song.mp3").ShouldBeFalse();
    }

    [Fact]
    public void Undo_FileNoLongerAtTarget_DoesNothing()
    {
        var fileSystem = new MockFileSystem();
        var move = new FileMove("/repo/Old/Song.mp3", "/repo/New/Song.mp3");

        move.Undo(fileSystem);

        fileSystem.File.Exists("/repo/Old/Song.mp3").ShouldBeFalse();
    }
}
