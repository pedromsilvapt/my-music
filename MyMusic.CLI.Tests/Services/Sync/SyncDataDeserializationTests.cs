namespace MyMusic.CLI.Tests.Services.Sync;

using System.Text.Json;
using MyMusic.CLI.Services.Sync;
using Shouldly;
using Xunit;

/// <summary>
/// Pins how the free-form <c>Data</c> payload of sync records is parsed, regardless of how the JSON metadata is produced
/// (reflection or source generation).
/// </summary>
public class SyncDataDeserializationTests
{
    private static readonly DateTime Modified = new(2024, 1, 2, 10, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Created = new(2023, 5, 6, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Synced = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string?> NoData => new() { null, "null" };

    public static TheoryData<string> InvalidRenameData => new()
    {
        "{}",
        """{"previousPath":"old.mp3"}""",
        """{"previousPath":5,"newPath":"new.mp3"}""",
        "[]",
        "\"text\"",
    };

    public static TheoryData<string> InvalidCheckData => new()
    {
        """{"modifiedAt":"not a date","createdAt":"2023-05-06T08:00:00Z"}""",
        """{"modifiedAt":"2024-01-02T10:30:00Z"}""",
        "[]",
        "42",
    };

    private static JsonElement? Parse(string? json) =>
        json == null ? null : JsonDocument.Parse(json).RootElement.Clone();

    #region RenameData

    [Theory]
    [InlineData("""{"previousPath":"old/song.mp3","newPath":"new/song.mp3"}""")]
    [InlineData("""{"PreviousPath":"old/song.mp3","NewPath":"new/song.mp3"}""")]
    [InlineData("""{"PREVIOUSPATH":"old/song.mp3","newpath":"new/song.mp3","extra":true}""")]
    public void DeserializeRenameData_AnyPropertyCasing_ReturnsPaths(string json)
    {
        var result = Phases.DeserializeRenameData(Parse(json));

        result.ShouldNotBeNull();
        result.PreviousPath.ShouldBe("old/song.mp3");
        result.NewPath.ShouldBe("new/song.mp3");
    }

    [Theory]
    [MemberData(nameof(NoData))]
    public void DeserializeRenameData_NoData_ReturnsNull(string? json) =>
        Phases.DeserializeRenameData(Parse(json)).ShouldBeNull();

    [Theory]
    [MemberData(nameof(InvalidRenameData))]
    public void DeserializeRenameData_InvalidData_ReturnsNull(string json) =>
        Phases.DeserializeRenameData(Parse(json)).ShouldBeNull();

    #endregion

    #region SyncCheckCreateUpdateData

    [Theory]
    [InlineData("""{"modifiedAt":"2024-01-02T10:30:00Z","createdAt":"2023-05-06T08:00:00Z","reason":"new"}""")]
    [InlineData("""{"ModifiedAt":"2024-01-02T10:30:00Z","CreatedAt":"2023-05-06T08:00:00Z","Reason":"new"}""")]
    public void DeserializeCheckCreateUpdateData_AnyPropertyCasing_ReturnsValues(string json)
    {
        var result = Phases.DeserializeCheckCreateUpdateData(Parse(json));

        result.ShouldNotBeNull();
        result.ModifiedAt.ShouldBe(Modified);
        result.ModifiedAt.Kind.ShouldBe(DateTimeKind.Utc);
        result.CreatedAt.ShouldBe(Created);
        result.Reason.ShouldBe("new");
    }

    [Fact]
    public void DeserializeCheckCreateUpdateData_WithoutReason_ReturnsNullReason()
    {
        var result = Phases.DeserializeCheckCreateUpdateData(
            Parse("""{"modifiedAt":"2024-01-02T10:30:00Z","createdAt":"2023-05-06T08:00:00Z"}"""));

        result.ShouldNotBeNull();
        result.Reason.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(NoData))]
    public void DeserializeCheckCreateUpdateData_NoData_ReturnsNull(string? json) =>
        Phases.DeserializeCheckCreateUpdateData(Parse(json)).ShouldBeNull();

    [Theory]
    [MemberData(nameof(InvalidCheckData))]
    public void DeserializeCheckCreateUpdateData_InvalidData_ReturnsNull(string json) =>
        Phases.DeserializeCheckCreateUpdateData(Parse(json)).ShouldBeNull();

    #endregion

    #region ConflictCheckData

    [Theory]
    [InlineData("""{"localModifiedAt":"2024-01-02T10:30:00Z","serverModifiedAt":"2023-05-06T08:00:00Z"}""")]
    [InlineData("""{"LocalModifiedAt":"2024-01-02T10:30:00Z","ServerModifiedAt":"2023-05-06T08:00:00Z"}""")]
    public void DeserializeConflictCheckData_AnyPropertyCasing_ReturnsValues(string json)
    {
        var result = SyncDataDeserialization.DeserializeConflictCheckData(Parse(json));

        result.ShouldNotBeNull();
        result.LocalModifiedAt.ShouldBe(Modified);
        result.LocalModifiedAt.Kind.ShouldBe(DateTimeKind.Utc);
        result.ServerModifiedAt.ShouldBe(Created);
    }

    [Fact]
    public void DeserializeConflictCheckData_MissingProperties_UsesDefaults()
    {
        var result = SyncDataDeserialization.DeserializeConflictCheckData(Parse("{}"));

        result.ShouldNotBeNull();
        result.LocalModifiedAt.ShouldBe(default);
        result.ServerModifiedAt.ShouldBe(default);
    }

    [Fact]
    public void DeserializeConflictCheckData_ServerChecksumAlgorithm_IsReadWhenPresent()
    {
        SyncDataDeserialization.DeserializeConflictCheckData(Parse("""{"serverChecksumAlgorithm":"XxHash128"}"""))!
            .ServerChecksumAlgorithm.ShouldBe("XxHash128");
        SyncDataDeserialization.DeserializeConflictCheckData(Parse("{}"))!.ServerChecksumAlgorithm.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(NoData))]
    public void DeserializeConflictCheckData_NoData_ReturnsNull(string? json) =>
        SyncDataDeserialization.DeserializeConflictCheckData(Parse(json)).ShouldBeNull();

    [Theory]
    [InlineData("""{"localModifiedAt":"not a date","serverModifiedAt":"2023-05-06T08:00:00Z"}""")]
    [InlineData("[]")]
    [InlineData("42")]
    public void DeserializeConflictCheckData_InvalidData_ReturnsNull(string json) =>
        SyncDataDeserialization.DeserializeConflictCheckData(Parse(json)).ShouldBeNull();

    #endregion

    #region UpdateLocalCheckData

    [Theory]
    [InlineData("""{"localModifiedAt":"2024-01-02T10:30:00Z","serverModifiedAt":"2023-05-06T08:00:00Z","lastSyncedAt":"2024-01-01T00:00:00Z"}""")]
    [InlineData("""{"LocalModifiedAt":"2024-01-02T10:30:00Z","ServerModifiedAt":"2023-05-06T08:00:00Z","LastSyncedAt":"2024-01-01T00:00:00Z"}""")]
    public void DeserializeUpdateLocalCheckData_AnyPropertyCasing_ReturnsValues(string json)
    {
        var result = SyncDataDeserialization.DeserializeUpdateLocalCheckData(Parse(json));

        result.ShouldNotBeNull();
        result.LocalModifiedAt.ShouldBe(Modified);
        result.ServerModifiedAt.ShouldBe(Created);
        result.LastSyncedAt.ShouldBe(Synced);
        result.LastSyncedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void DeserializeUpdateLocalCheckData_MissingProperties_UsesDefaults()
    {
        var result = SyncDataDeserialization.DeserializeUpdateLocalCheckData(Parse("{}"));

        result.ShouldNotBeNull();
        result.LocalModifiedAt.ShouldBe(default);
        result.ServerModifiedAt.ShouldBe(default);
        result.LastSyncedAt.ShouldBe(default);
    }

    [Fact]
    public void DeserializeUpdateLocalCheckData_ServerChecksumAlgorithm_IsReadWhenPresent()
    {
        SyncDataDeserialization.DeserializeUpdateLocalCheckData(Parse("""{"serverChecksumAlgorithm":"XxHash128"}"""))!
            .ServerChecksumAlgorithm.ShouldBe("XxHash128");
        SyncDataDeserialization.DeserializeUpdateLocalCheckData(Parse("{}"))!.ServerChecksumAlgorithm.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(NoData))]
    public void DeserializeUpdateLocalCheckData_NoData_ReturnsNull(string? json) =>
        SyncDataDeserialization.DeserializeUpdateLocalCheckData(Parse(json)).ShouldBeNull();

    [Theory]
    [InlineData("""{"localModifiedAt":"not a date"}""")]
    [InlineData("[]")]
    [InlineData("42")]
    public void DeserializeUpdateLocalCheckData_InvalidData_ReturnsNull(string json) =>
        SyncDataDeserialization.DeserializeUpdateLocalCheckData(Parse(json)).ShouldBeNull();

    #endregion
}
