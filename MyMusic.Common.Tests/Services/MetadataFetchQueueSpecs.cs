using System.Text.Json;
using MyMusic.Common.Entities;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class MetadataFetchQueueSpecs
{
    [Fact]
    public void MetadataFetchTask_WithRequiredFields_CanBeCreated()
    {
        // Arrange & Act
        var task = new MetadataFetchTask
        {
            SongId = 1,
            Status = MetadataFetchStatus.Queued,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
        };

        // Assert
        task.ShouldNotBeNull();
        task.Status.ShouldBe(MetadataFetchStatus.Queued);
        task.Progress.ShouldBe(0);
        task.SongId.ShouldBe(1);
    }

    [Fact]
    public void MetadataFetchTask_QueuedStatus_TransitionsToProcessing()
    {
        // Arrange
        var task = new MetadataFetchTask
        {
            SongId = 1,
            Status = MetadataFetchStatus.Queued,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
        };

        // Act
        task.Status = MetadataFetchStatus.Processing;
        task.Progress = 50;
        task.StartedAt = DateTime.UtcNow;

        // Assert
        task.Status.ShouldBe(MetadataFetchStatus.Processing);
        task.Progress.ShouldBe(50);
        task.StartedAt.ShouldNotBeNull();
    }

    [Fact]
    public void MetadataFetchTask_ProcessingStatus_CanBeMarkedAsCompleted()
    {
        // Arrange
        var task = new MetadataFetchTask
        {
            SongId = 1,
            Status = MetadataFetchStatus.Processing,
            Progress = 50,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
        };

        // Act
        task.Status = MetadataFetchStatus.Completed;
        task.Progress = 100;
        task.CompletedAt = DateTime.UtcNow;

        // Assert
        task.Status.ShouldBe(MetadataFetchStatus.Completed);
        task.Progress.ShouldBe(100);
        task.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public void MetadataFetchTask_ProcessingStatus_CanBeMarkedAsFailed()
    {
        // Arrange
        var task = new MetadataFetchTask
        {
            SongId = 1,
            Status = MetadataFetchStatus.Processing,
            Progress = 75,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
        };

        // Act
        task.Status = MetadataFetchStatus.Failed;
        task.ErrorMessage = "Service unavailable";
        task.CompletedAt = DateTime.UtcNow;

        // Assert
        task.Status.ShouldBe(MetadataFetchStatus.Failed);
        task.ErrorMessage.ShouldBe("Service unavailable");
        task.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public void AutoFetchedMetadata_WithJsonPatch_CanStoreSourceData()
    {
        // Arrange
        var sourceData = new { Title = "Updated Title", Year = 2024 };
        var jsonElement = JsonSerializer.SerializeToElement(sourceData);

        // Act
        var metadata = new AutoFetchedMetadata
        {
            SongId = 1,
            SourceMetadata = jsonElement,
            Status = AutoFetchStatus.Pending,
            FetchedAt = DateTime.UtcNow,
        };

        // Assert
        metadata.ShouldNotBeNull();
        metadata.SourceMetadata.GetProperty("Title").GetString().ShouldBe("Updated Title");
        metadata.SourceMetadata.GetProperty("Year").GetInt32().ShouldBe(2024);
    }

    [Fact]
    public void AutoFetchedMetadata_PendingStatus_TransitionsToApplied()
    {
        // Arrange
        var metadata = new AutoFetchedMetadata
        {
            SongId = 1,
            SourceMetadata = JsonSerializer.SerializeToElement(new { }),
            Status = AutoFetchStatus.Pending,
            FetchedAt = DateTime.UtcNow,
        };

        // Act
        metadata.Status = AutoFetchStatus.Applied;

        // Assert
        metadata.Status.ShouldBe(AutoFetchStatus.Applied);
    }

    [Fact]
    public void AutoFetchedMetadata_PendingStatus_CanBeMarkedAsFailed()
    {
        // Arrange
        var metadata = new AutoFetchedMetadata
        {
            SongId = 1,
            SourceMetadata = JsonSerializer.SerializeToElement(new { }),
            Status = AutoFetchStatus.Pending,
            FetchedAt = DateTime.UtcNow,
        };

        // Act
        metadata.Status = AutoFetchStatus.Failed;
        metadata.ErrorMessage = "Network error occurred";

        // Assert
        metadata.Status.ShouldBe(AutoFetchStatus.Failed);
        metadata.ErrorMessage.ShouldBe("Network error occurred");
    }

    [Fact]
    public void AutoFetchedMetadata_WithSourceId_CanTrackSource()
    {
        // Arrange
        var metadata = new AutoFetchedMetadata
        {
            SongId = 1,
            SourceMetadata = JsonSerializer.SerializeToElement(new { Title = "Test" }),
            Status = AutoFetchStatus.Pending,
            FetchedAt = DateTime.UtcNow,
            SourceId = 42,
        };

        // Assert
        metadata.SourceId.ShouldBe(42);
    }

    [Fact]
    public void MetadataFetchStatus_Enum_HasExpectedValues()
    {
        // Assert
        ((int)MetadataFetchStatus.Queued).ShouldBe(0);
        ((int)MetadataFetchStatus.Processing).ShouldBe(1);
        ((int)MetadataFetchStatus.Completed).ShouldBe(2);
        ((int)MetadataFetchStatus.Failed).ShouldBe(3);
    }

    [Fact]
    public void AutoFetchStatus_Enum_HasExpectedValues()
    {
        // Assert
        ((int)AutoFetchStatus.Pending).ShouldBe(0);
        ((int)AutoFetchStatus.Applied).ShouldBe(1);
        ((int)AutoFetchStatus.Failed).ShouldBe(2);
        ((int)AutoFetchStatus.Expired).ShouldBe(3);
    }
}
