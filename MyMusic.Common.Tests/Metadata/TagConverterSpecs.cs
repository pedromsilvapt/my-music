using System.IO.Abstractions.TestingHelpers;
using MyMusic.Common.Metadata;
using MyMusic.Common.Tests.Utilities;
using MyMusic.Common.Targets;
using Shouldly;

namespace MyMusic.Common.Tests.Metadata;

public class TagConverterSpecs
{
    [Fact]
    public void ToSong_ExtractsBitrateFromProperties()
    {
        // Arrange
        var fs = new MockFileSystem();
        var filePath = "/test/song.mp3";
        
        MockMusicFile.Create(fs, filePath, "Test Song", "Test Album", ["Test Artist"], ["Rock"]);
        
        var fileInfo = new FileSystemFileAbstraction(fs.FileInfo.New(filePath));
        using var tfile = TagLib.File.Create(fileInfo);
        
        // Act
        var metadata = TagConverter.ToSong(tfile.Tag, tfile.Properties);
        
        // Assert
        metadata.Bitrate.ShouldNotBeNull();
        metadata.Bitrate.Value.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public void ToSong_ExtractsOtherMetadata()
    {
        // Arrange
        var fs = new MockFileSystem();
        var filePath = "/test/song.mp3";
        
        MockMusicFile.Create(fs, filePath, "Test Song", "Test Album", ["Test Artist"], ["Rock"], 2023);
        
        var fileInfo = new FileSystemFileAbstraction(fs.FileInfo.New(filePath));
        using var tfile = TagLib.File.Create(fileInfo);
        
        // Act
        var metadata = TagConverter.ToSong(tfile.Tag, tfile.Properties);
        
        // Assert
        metadata.Title.ShouldBe("Test Song");
        metadata.Album?.Name.ShouldBe("Test Album");
        metadata.Artists?.Select(a => a.Name).ShouldBe(["Test Artist"]);
        metadata.Genres?.ShouldBe(["Rock"]);
        metadata.Year.ShouldBe(2023);
        metadata.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        metadata.Bitrate.ShouldNotBeNull();
    }
}
