namespace MyMusic.CLI.Tests.Services.Devices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using MyMusic.CLI.Services.Devices;
using NSubstitute;
using Shouldly;
using Xunit;

public class DeviceConfigServiceTests
{
    private const string LocalTemplate = "{{ year }}/{{ simple_label }}.mp3";

    private readonly IMyMusicClient _client = Substitute.For<IMyMusicClient>();

    private DeviceConfigService CreateService()
    {
        var options = new MyMusicOptions();
        options.Device.Name = "Desktop";
        options.Device.Icon = "IconDevicesPc";
        options.Device.NamingTemplate = LocalTemplate;

        return new DeviceConfigService(Options.Create(options), _client, Substitute.For<ILogger<DeviceConfigService>>());
    }

    private void GivenServerDevice(string? namingTemplate)
    {
        _client.GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(new ListDevicesResponse
        {
            Devices =
            [
                new ListDeviceItem { Id = 7, Name = "Desktop", SongCount = 0, Icon = "IconDevicesPc", NamingTemplate = namingTemplate }
            ]
        });
    }

    [Fact]
    public async Task Resolve_SaveOptions_UpdatesDeviceThatDiffers()
    {
        GivenServerDevice(namingTemplate: "{{ simple_label }}.mp3");

        var result = await CreateService().ResolveAsync(saveOptions: true);

        result.DeviceId.ShouldBe(7);
        result.Outcome.ShouldBe(DeviceConfigOutcome.Updated);
        await _client.Received(1).UpdateDeviceAsync(7,
            Arg.Is<UpdateDeviceRequest>(r => r.NamingTemplate == LocalTemplate && r.Icon == "IconDevicesPc"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_WithoutSaveOptions_LeavesDeviceThatDiffersUntouched()
    {
        GivenServerDevice(namingTemplate: "{{ simple_label }}.mp3");

        var result = await CreateService().ResolveAsync(saveOptions: false);

        result.DeviceId.ShouldBe(7);
        result.Outcome.ShouldBe(DeviceConfigOutcome.Unchanged);
        await _client.DidNotReceive().UpdateDeviceAsync(Arg.Any<long>(), Arg.Any<UpdateDeviceRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_SaveOptions_DeviceUpToDate_DoesNotUpdate()
    {
        GivenServerDevice(namingTemplate: LocalTemplate);

        var result = await CreateService().ResolveAsync(saveOptions: true);

        result.Outcome.ShouldBe(DeviceConfigOutcome.Unchanged);
        await _client.DidNotReceive().UpdateDeviceAsync(Arg.Any<long>(), Arg.Any<UpdateDeviceRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_MissingDevice_CreatesItWithTheConfiguredOptions(bool saveOptions)
    {
        _client.GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(new ListDevicesResponse { Devices = [] });
        _client.CreateDeviceAsync(Arg.Any<CreateDeviceRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateDeviceResponse { Device = new CreateDeviceItem { Id = 9, Name = "Desktop" } });

        var result = await CreateService().ResolveAsync(saveOptions);

        result.DeviceId.ShouldBe(9);
        result.Outcome.ShouldBe(DeviceConfigOutcome.Created);
        await _client.Received(1).CreateDeviceAsync(
            Arg.Is<CreateDeviceRequest>(r => r.Name == "Desktop" && r.NamingTemplate == LocalTemplate),
            Arg.Any<CancellationToken>());
    }
}
