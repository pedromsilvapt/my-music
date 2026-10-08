namespace MyMusic.CLI.Tests.Services.Sync;

using Microsoft.Extensions.Options;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services.Devices;
using MyMusic.CLI.Services.Sync;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

public class CliSyncConfigTests
{
    private readonly IDeviceConfigService _deviceConfig = Substitute.For<IDeviceConfigService>();

    private CliSyncConfig CreateConfig() => new(Options.Create(new MyMusicOptions()), _deviceConfig);

    [Fact]
    public async Task GetDeviceId_ResolvesTheDeviceOnce()
    {
        _deviceConfig.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(new ListDeviceItem { Id = 7, Name = "Desktop", SongCount = 0 });
        var config = CreateConfig();

        var first = await config.GetDeviceIdAsync();
        var second = await config.GetDeviceIdAsync();

        first.ShouldBe(7);
        second.ShouldBe(7);
        await _deviceConfig.Received(1).ResolveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDeviceId_DeviceNotFound_FailsWithTheReason()
    {
        // The message tells the user to create the device in the web app, so it must reach the command
        _deviceConfig.ResolveAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DeviceNotFoundException("Desktop"));

        var exception = await Should.ThrowAsync<DeviceNotFoundException>(() => CreateConfig().GetDeviceIdAsync());

        exception.Message.ShouldContain("Create it in the web app");
    }
}
