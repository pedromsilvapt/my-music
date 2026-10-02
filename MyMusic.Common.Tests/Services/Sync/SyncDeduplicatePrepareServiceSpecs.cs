using MyMusic.Common.Entities;
using MyMusic.Common.Services.Sync;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Sync;

public class SyncDeduplicatePrepareServiceSpecs
{
    private readonly Scenario _scenario = new();
    private readonly ISyncSoundalikeMatcher _matcher = Substitute.For<ISyncSoundalikeMatcher>();
    private readonly SyncDeduplicatePrepareService _service;
    private readonly Device _device;

    public SyncDeduplicatePrepareServiceSpecs()
    {
        _service = new SyncDeduplicatePrepareService(_scenario.DbContext, new SyncSessionLookupService(), _matcher);
        _device = _scenario.CreateDevice("Phone");
    }

    private DeviceSyncSession CreateSession(bool deduplicate, SyncSessionStatus status = SyncSessionStatus.InProgress)
    {
        var session = _scenario.CreateSession(_device, status);
        session.Deduplicate = deduplicate;
        _scenario.DbContext.SaveChanges();
        return session;
    }

    private Task<SyncLibraryPreparation?> PrepareAsync(long sessionId, long? deviceId = null) =>
        _service.PrepareAsync(deviceId ?? _device.Id, sessionId, _scenario.AdminUser.Id, CancellationToken.None);

    [Fact]
    public async Task PrepareAsync_DeduplicateSession_PreparesOneBatchOfTheLibrary()
    {
        var session = CreateSession(deduplicate: true);
        var preparation = new SyncLibraryPreparation(50, 20, false);
        _matcher.PrepareLibraryAsync(session.Id, _scenario.AdminUser.Id, SyncDeduplicatePrepareService.BatchSize, Arg.Any<CancellationToken>())
            .Returns(preparation);

        (await PrepareAsync(session.Id)).ShouldBe(preparation);
    }

    [Fact]
    public async Task PrepareAsync_UnknownSession_ReturnsNull()
    {
        var session = CreateSession(deduplicate: true);

        (await PrepareAsync(session.Id + 1)).ShouldBeNull();
        (await PrepareAsync(session.Id, deviceId: _device.Id + 1)).ShouldBeNull();
        await _matcher.DidNotReceiveWithAnyArgs().PrepareLibraryAsync(default, default, default, default);
    }

    [Fact]
    public async Task PrepareAsync_SessionWithoutDeduplicate_Throws()
    {
        var session = CreateSession(deduplicate: false);

        await Should.ThrowAsync<SyncDeduplicatePrepareValidationException>(() => PrepareAsync(session.Id));
        await _matcher.DidNotReceiveWithAnyArgs().PrepareLibraryAsync(default, default, default, default);
    }

    [Fact]
    public async Task PrepareAsync_SessionNotInProgress_Throws()
    {
        var session = CreateSession(deduplicate: true, SyncSessionStatus.Committed);

        await Should.ThrowAsync<SyncDeduplicatePrepareValidationException>(() => PrepareAsync(session.Id));
    }
}
