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
    private readonly IMyMusicClient _client = Substitute.For<IMyMusicClient>();

    private DeviceConfigService CreateService(string deviceName = "Desktop")
    {
        var options = new MyMusicOptions();
        options.Device.Name = deviceName;

        return new DeviceConfigService(Options.Create(options), _client, Substitute.For<ILogger<DeviceConfigService>>());
    }

    private void GivenServerDevices(params ListDeviceItem[] devices)
    {
        _client.GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(new ListDevicesResponse
        {
            Devices = [.. devices]
        });
    }

    [Fact]
    public async Task Resolve_DeviceWithConfiguredName_ReturnsIt()
    {
        GivenServerDevices(
            new ListDeviceItem { Id = 3, Name = "Laptop", SongCount = 0 },
            new ListDeviceItem
            {
                Id = 7,
                Name = "Desktop",
                SongCount = 12,
                Icon = "IconDevicesPc",
                NamingTemplate = "{{ simple_label }}.mp3",
                ImportOnPurchase = true,
            });

        var device = await CreateService().ResolveAsync();

        device.Id.ShouldBe(7);
        device.Name.ShouldBe("Desktop");
        device.Icon.ShouldBe("IconDevicesPc");
        device.NamingTemplate.ShouldBe("{{ simple_label }}.mp3");
        device.ImportOnPurchase.ShouldBeTrue();
        device.SongCount.ShouldBe(12);
    }

    [Fact]
    public async Task Resolve_NoDeviceWithConfiguredName_ThrowsTellingToCreateItOnTheWeb()
    {
        GivenServerDevices(new ListDeviceItem { Id = 3, Name = "Laptop", SongCount = 0 });

        var exception = await Should.ThrowAsync<DeviceNotFoundException>(() => CreateService().ResolveAsync());

        exception.Message.ShouldBe("Device 'Desktop' not found. Create it in the web app (Devices > New device)");
    }

    [Fact]
    public async Task Resolve_NameDiffersInCase_Throws()
    {
        // Device names are matched exactly, as the server stores them
        GivenServerDevices(new ListDeviceItem { Id = 7, Name = "desktop", SongCount = 0 });

        await Should.ThrowAsync<DeviceNotFoundException>(() => CreateService().ResolveAsync());
    }

    [Fact]
    public async Task Resolve_NeverChangesTheServer()
    {
        GivenServerDevices(new ListDeviceItem { Id = 7, Name = "Desktop", SongCount = 0 });

        await CreateService().ResolveAsync();

        // Devices are created and edited in the web app: the only call is the lookup
        _client.ReceivedCalls().Count().ShouldBe(1);
    }
}
