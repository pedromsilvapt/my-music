using System.IO.Abstractions.TestingHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Sync;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class DevicesControllerUploadSpecs
{
    private DevicesController CreateController(Scenario scenario, ISyncActionsServerFactory? factory = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);

        var config = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
        config["MyMusic:MusicRepositoryPath"].Returns("/data");

        var syncUploadService = new SyncUploadService(
            scenario.DbContext,
            scenario.FileSystem,
            scenario.CreateMusicService(),
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ILogger<SyncUploadService>>());

        return new DevicesController(
            Substitute.For<ILogger<DevicesController>>(),
            currentUser,
            scenario.DbContext,
            config,
            Substitute.For<Microsoft.Extensions.Options.IOptions<Config>>(),
            scenario.FileSystem,
            factory ?? Substitute.For<ISyncActionsServerFactory>(),
            Substitute.For<ISyncCommitService>(),
            syncUploadService
        );
    }


    [Fact]
    public async Task UploadFile_NewFile_SetsIsUpdateFalseAndMapsCreateRemoteResponse()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");

        var controller = CreateController(scenario, factory);
        var formFile = Scenario.CreateMockFormFile(new byte[] { 1, 2, 3, 4, 5 });
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
        response.Value.Action.ShouldBe(SyncRecordAction.CreateRemote.ToString());
        response.Value.SongId.ShouldBeNull();
        response.Value.RecordId.ShouldNotBeNull();
    }

    [Fact]
    public async Task UploadFile_ExistingDevice_SetsIsUpdateTrueAndMapsUpdateRemoteResponse()
    {
        var scenario = new Scenario();
        var factory = new SyncActionsServerFactory();
        var song = scenario.CreateSong("Song");
        var device = scenario.CreateDevice();
        var session = scenario.CreateSession(device, repositoryPath: "/data");
        scenario.CreateSongDevice(device, song, "/music/song.mp3");

        var controller = CreateController(scenario, factory);
        var formFile = Scenario.CreateMockFormFile(new byte[] { 1, 2, 3, 4, 5 });
        var modifiedAt = DateTime.UtcNow.ToString("O");
        var createdAt = DateTime.UtcNow.ToString("O");

        var response = await controller.UploadFile(device.Id, session.Id, formFile, "/music/song.mp3", modifiedAt, createdAt, CancellationToken.None);

        response.Value.Success.ShouldBeTrue();
        response.Value.Action.ShouldBe(SyncRecordAction.UpdateRemote.ToString());
        response.Value.SongId.ShouldBe(song.Id);
        response.Value.RecordId.ShouldNotBeNull();
    }
}