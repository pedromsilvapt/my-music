using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceUpdateServiceSpecs
{
    private static (DeviceUpdateService service, ICurrentUser currentUser) CreateService(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var service = new DeviceUpdateService(
            scenario.DbContext,
            new DeviceLookupService(),
            currentUser,
            Substitute.For<ILogger<DeviceUpdateService>>());
        return (service, currentUser);
    }

    private static DeviceUpdateInput Request(
        string? icon = "newicon",
        string? color = "#000",
        string? namingTemplate = "{title}",
        bool? importOnPurchase = true) =>
        new()
        {
            Icon = icon,
            Color = color,
            NamingTemplate = namingTemplate,
            ImportOnPurchase = importOnPurchase,
        };

    [Fact]
    public async Task Update_OwnedDevice_UpdatesFieldsAndPersists()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone", namingTemplate: "{artist}/{title}");
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.UpdateAsync(device.Id, Request(), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Device.Id.ShouldBe(device.Id);
        result.Device.Name.ShouldBe("Phone");
        result.Device.Icon.ShouldBe("newicon");
        result.Device.Color.ShouldBe("#000");
        result.Device.NamingTemplate.ShouldBe("{title}");
        result.Device.ImportOnPurchase.ShouldBeTrue();

        var stored = scenario.DbContext.Devices.Single(d => d.Id == device.Id);
        stored.Icon.ShouldBe("newicon");
        stored.Color.ShouldBe("#000");
        stored.NamingTemplate.ShouldBe("{title}");
        stored.ImportOnPurchase.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_OtherUsersDevice_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var otherDevice = scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id, namingTemplate: "{title}");
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.UpdateAsync(otherDevice.Id, Request(), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
        // Original values preserved
        var stored = scenario.DbContext.Devices.Single(d => d.Id == otherDevice.Id);
        stored.NamingTemplate.ShouldBe("{title}");
        stored.Icon.ShouldBeNull();
    }

    [Fact]
    public async Task Update_UnknownDeviceId_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.UpdateAsync(9999, Request(), CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ImportOnPurchaseNotProvided_PreservesExistingValue()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        // Toggle ImportOnPurchase to true directly so we can verify it is preserved.
        device.ImportOnPurchase = true;
        scenario.DbContext.SaveChanges();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.UpdateAsync(
            device.Id,
            Request(importOnPurchase: null),
            CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Device.ImportOnPurchase.ShouldBeTrue();
        var stored = scenario.DbContext.Devices.Single(d => d.Id == device.Id);
        stored.ImportOnPurchase.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_ImportOnPurchaseFalse_OverwritesExistingTrue()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone");
        device.ImportOnPurchase = true;
        scenario.DbContext.SaveChanges();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.UpdateAsync(
            device.Id,
            Request(importOnPurchase: false),
            CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Device.ImportOnPurchase.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_NullableFieldsSetToNull_ClearsStoredValues()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("Phone", namingTemplate: "{title}");
        device.Icon = "oldicon";
        device.Color = "#123";
        scenario.DbContext.SaveChanges();
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.UpdateAsync(
            device.Id,
            Request(icon: null, color: null, namingTemplate: null, importOnPurchase: null),
            CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Device.Icon.ShouldBeNull();
        result.Device.Color.ShouldBeNull();
        result.Device.NamingTemplate.ShouldBeNull();

        var stored = scenario.DbContext.Devices.Single(d => d.Id == device.Id);
        stored.Icon.ShouldBeNull();
        stored.Color.ShouldBeNull();
        stored.NamingTemplate.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ResponseReflectsPersistedNameUnchanged()
    {
        // Arrange
        var scenario = new Scenario();
        var device = scenario.CreateDevice("MyPhone");
        var (service, _) = CreateService(scenario);

        // Act
        var result = await service.UpdateAsync(device.Id, Request(), CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Device.Name.ShouldBe("MyPhone");
        // Name is not part of the update request and must remain unchanged.
        var stored = scenario.DbContext.Devices.Single(d => d.Id == device.Id);
        stored.Name.ShouldBe("MyPhone");
    }
}