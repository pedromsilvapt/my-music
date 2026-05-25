using MyMusic.Common.Services.Sync;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncActionDataSpecs
{
    #region CreateRemoteData

    [Fact]
    public void CreateRemoteData_RoundTrips_ThroughJsonSerializer()
    {
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2025, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var data = new CreateRemoteData
        {
            SongId = 42,
            Checksum = "abc123",
            Algorithm = "XxHash128",
            ModifiedAt = modifiedAt,
            TempFilePath = "/tmp/file.mp3",
            CreatedAt = createdAt,
            OriginalFilePath = "/original/file.mp3",
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<CreateRemoteData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBe(42);
        result.Checksum.ShouldBe("abc123");
        result.Algorithm.ShouldBe("XxHash128");
        result.ModifiedAt.ShouldBe(modifiedAt);
        result.TempFilePath.ShouldBe("/tmp/file.mp3");
        result.CreatedAt.ShouldBe(createdAt);
        result.OriginalFilePath.ShouldBe("/original/file.mp3");
    }

    [Fact]
    public void CreateRemoteData_RoundTrips_WithNullOptionals()
    {
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var data = new CreateRemoteData
        {
            SongId = null,
            Checksum = "abc",
            Algorithm = "XxHash128",
            ModifiedAt = modifiedAt,
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<CreateRemoteData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBeNull();
        result.TempFilePath.ShouldBeNull();
        result.CreatedAt.ShouldBeNull();
        result.OriginalFilePath.ShouldBeNull();
    }

    [Fact]
    public void CreateRemoteData_DeserializesFromSyncActionsServerOutput()
    {
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var serverData = new CreateRemoteData
        {
            SongId = 42,
            Checksum = "abc123",
            Algorithm = "XxHash128",
            ModifiedAt = modifiedAt,
            TempFilePath = "/tmp/file.mp3",
            CreatedAt = null,
            OriginalFilePath = null,
        };
        var element = SyncActionDataSerializer.Serialize(serverData);
        var result = SyncActionDataSerializer.Deserialize<CreateRemoteData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBe(42);
        result.Checksum.ShouldBe("abc123");
        result.Algorithm.ShouldBe("XxHash128");
        result.TempFilePath.ShouldBe("/tmp/file.mp3");
    }

    #endregion

    #region UpdateRemoteData

    [Fact]
    public void UpdateRemoteData_RoundTrips_ThroughJsonSerializer()
    {
        var modifiedAt = new DateTime(2025, 7, 15, 8, 30, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var data = new UpdateRemoteData
        {
            SongId = 99,
            Checksum = "def456",
            Algorithm = "SHA256",
            ModifiedAt = modifiedAt,
            TempFilePath = "/tmp/update.mp3",
            CreatedAt = createdAt,
            OriginalFilePath = "/original/update.mp3",
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<UpdateRemoteData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBe(99);
        result.Checksum.ShouldBe("def456");
        result.Algorithm.ShouldBe("SHA256");
        result.ModifiedAt.ShouldBe(modifiedAt);
        result.TempFilePath.ShouldBe("/tmp/update.mp3");
        result.CreatedAt.ShouldBe(createdAt);
        result.OriginalFilePath.ShouldBe("/original/update.mp3");
    }

    #endregion

    #region SongModifiedAtData

    [Fact]
    public void SongModifiedAtData_RoundTrips_WithAllFields()
    {
        var modifiedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var data = new SongModifiedAtData
        {
            SongId = 10,
            ModifiedAt = modifiedAt,
            Checksum = "xyz789",
            Algorithm = "MD5",
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBe(10);
        result.ModifiedAt.ShouldBe(modifiedAt);
        result.Checksum.ShouldBe("xyz789");
        result.Algorithm.ShouldBe("MD5");
    }

    [Fact]
    public void SongModifiedAtData_RoundTrips_WithOnlySongId()
    {
        var data = new SongModifiedAtData { SongId = 5 };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBe(5);
        result.ModifiedAt.ShouldBeNull();
        result.Checksum.ShouldBeNull();
        result.Algorithm.ShouldBeNull();
    }

    [Fact]
    public void SongModifiedAtData_RoundTrips_WithChecksumAndAlgorithm()
    {
        var modifiedAt = new DateTime(2025, 8, 20, 14, 30, 0, DateTimeKind.Utc);
        var data = new SongModifiedAtData
        {
            Checksum = "abc",
            Algorithm = "XxHash128",
            ModifiedAt = modifiedAt,
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.Checksum.ShouldBe("abc");
        result.Algorithm.ShouldBe("XxHash128");
        result.ModifiedAt.ShouldBe(modifiedAt);
        result.SongId.ShouldBeNull();
    }

    [Fact]
    public void SongModifiedAtData_DeserializesNullElement()
    {
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(null);
        result.ShouldBeNull();
    }

    #endregion

    #region RenameData

    [Fact]
    public void RenameData_RoundTrips_ThroughJsonSerializer()
    {
        var data = new RenameData { PreviousPath = "/music/old.mp3", NewPath = "/music/new.mp3" };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<RenameData>(element);

        result.ShouldNotBeNull();
        result.PreviousPath.ShouldBe("/music/old.mp3");
        result.NewPath.ShouldBe("/music/new.mp3");
    }

    #endregion

    #region ConflictData

    [Fact]
    public void ConflictData_RoundTrips_ThroughJsonSerializer()
    {
        var localAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var serverAt = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var data = new ConflictData
        {
            LocalModifiedAt = localAt,
            ServerModifiedAt = serverAt,
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<ConflictData>(element);

        result.ShouldNotBeNull();
        result.LocalModifiedAt.ShouldBe(localAt);
        result.ServerModifiedAt.ShouldBe(serverAt);
    }

    #endregion

    #region UpdateTimestampData

    [Fact]
    public void UpdateTimestampData_RoundTrips_ThroughJsonSerializer()
    {
        var timestamp = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var data = new UpdateTimestampData
        {
            NewTimestamp = timestamp,
            SongId = 42,
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<UpdateTimestampData>(element);

        result.ShouldNotBeNull();
        result.NewTimestamp.ShouldBe(timestamp);
        result.SongId.ShouldBe(42);
    }

    [Fact]
    public void UpdateTimestampData_RoundTrips_WithNullSongId()
    {
        var timestamp = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var data = new UpdateTimestampData
        {
            NewTimestamp = timestamp,
            SongId = null,
        };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<UpdateTimestampData>(element);

        result.ShouldNotBeNull();
        result.NewTimestamp.ShouldBe(timestamp);
        result.SongId.ShouldBeNull();
    }

    #endregion

    #region ErrorData

    [Fact]
    public void ErrorData_RoundTrips_ThroughJsonSerializer()
    {
        var element = SyncActionDataSerializer.Serialize(new ErrorData { ErrorMessage = "Something failed" });
        var result = SyncActionDataSerializer.Deserialize<ErrorData>(element);

        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Something failed");
    }

    #endregion

    #region Unlink/Delete Data

    [Fact]
    public void UnlinkData_WithSongId_RoundTrips()
    {
        var element = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = 42 });
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBe(42);
    }

    [Fact]
    public void UnlinkData_WithNullSongId_StoresNullSongId()
    {
        var data = new SongModifiedAtData { SongId = null };
        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBeNull();
    }

    [Fact]
    public void DeleteData_WithSongId_RoundTrips()
    {
        var element = SyncActionDataSerializer.Serialize(new SongModifiedAtData { SongId = 10 });
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBe(10);
    }

    [Fact]
    public void DeleteData_WithNullSongId_StoresNullSongId()
    {
        var data = new SongModifiedAtData { SongId = null };
        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.SongId.ShouldBeNull();
    }

    #endregion

    #region DateTime Handling

    [Fact]
    public void DateTime_PreservesUtcKind()
    {
        var utcTime = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var data = new SongModifiedAtData { SongId = 1, ModifiedAt = utcTime };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.ModifiedAt.ShouldNotBeNull();
        result.ModifiedAt.Value.Kind.ShouldBe(DateTimeKind.Utc);
        result.ModifiedAt.Value.ShouldBe(utcTime);
    }

    [Fact]
    public void DateTime_ConvertsLocalToUtc()
    {
        var localTime = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Local);
        var data = new SongModifiedAtData { SongId = 1, ModifiedAt = localTime.ToUniversalTime() };

        var element = SyncActionDataSerializer.Serialize(data);
        var result = SyncActionDataSerializer.Deserialize<SongModifiedAtData>(element);

        result.ShouldNotBeNull();
        result.ModifiedAt.ShouldNotBeNull();
        result.ModifiedAt.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    #endregion

    #region Null Data Handling

    [Fact]
    public void DeserializeCreateRemoteData_NullElement_ReturnsNull()
    {
        var result = SyncActionDataSerializer.Deserialize<CreateRemoteData>(null);
        result.ShouldBeNull();
    }

    [Fact]
    public void DeserializeUpdateRemoteData_NullElement_ReturnsNull()
    {
        var result = SyncActionDataSerializer.Deserialize<UpdateRemoteData>(null);
        result.ShouldBeNull();
    }

    [Fact]
    public void DeserializeRenameData_NullElement_ReturnsNull()
    {
        var result = SyncActionDataSerializer.Deserialize<RenameData>(null);
        result.ShouldBeNull();
    }

    [Fact]
    public void DeserializeConflictData_NullElement_ReturnsNull()
    {
        var result = SyncActionDataSerializer.Deserialize<ConflictData>(null);
        result.ShouldBeNull();
    }

    [Fact]
    public void DeserializeUpdateTimestampData_NullElement_ReturnsNull()
    {
        var result = SyncActionDataSerializer.Deserialize<UpdateTimestampData>(null);
        result.ShouldBeNull();
    }

    [Fact]
    public void DeserializeErrorData_NullElement_ReturnsNull()
    {
        var result = SyncActionDataSerializer.Deserialize<ErrorData>(null);
        result.ShouldBeNull();
    }

    #endregion
}