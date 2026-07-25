using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Devices;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceCreateServiceSpecs
{
    private static (DeviceCreateService service, ICurrentUser currentUser) CreateService(Scenario scenario)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(scenario.AdminUser.Id);
        var service = new DeviceCreateService(
            scenario.DbContext,
            currentUser,
            Substitute.For<ILogger<DeviceCreateService>>());
        return (service, currentUser);
    }

    [Fact]
    public async Task Create_ValidRequest_PersistsDeviceScopedToOwner()
    {
        // Arrange
        var scenario = new Scenario();
        var (service, currentUser) = CreateService(scenario);
        var request = new DeviceCreateInput
        {
            Name = "Phone",
            Icon = "phone",
            Color = "#fff",
            NamingTemplate = "{artist}/{album}/{title}",
            ImportOnPurchase = true,
        };

        // Act
        var result = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Device.Name.ShouldBe("Phone");
        result.Device.Icon.ShouldBe("phone");
        result.Device.Color.ShouldBe("#fff");
        result.Device.NamingTemplate.ShouldBe("{artist}/{album}/{title}");
        result.Device.ImportOnPurchase.ShouldBeTrue();
        result.Device.Id.ShouldBeGreaterThan(0);

        var stored = scenario.DbContext.Devices.Single(d => d.Id == result.Device.Id);
        stored.OwnerId.ShouldBe(currentUser.Id);
        stored.Name.ShouldBe("Phone");
    }

    [Fact]
    public async Task Create_RequestWithoutOptionalFields_UsesDefaults()
    {
        // Arrange
        var scenario = new Scenario();
        var (service, _) = CreateService(scenario);
        var request = new DeviceCreateInput
        {
            Name = "Basic Device",
            Icon = null,
            Color = null,
            NamingTemplate = null,
            ImportOnPurchase = false,
        };

        // Act
        var result = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Device.Icon.ShouldBeNull();
        result.Device.Color.ShouldBeNull();
        result.Device.NamingTemplate.ShouldBeNull();
        result.Device.ImportOnPurchase.ShouldBeFalse();
    }

    [Fact]
    public async Task Create_UnknownUser_ReturnsNull()
    {
        // Arrange
        var scenario = new Scenario();
        var currentUser = Substitute.For<ICurrentUser>();
        // Use a user id that does not exist in the database.
        currentUser.Id.Returns(9999L);
        var service = new DeviceCreateService(
            scenario.DbContext,
            currentUser,
            Substitute.For<ILogger<DeviceCreateService>>());
        var request = new DeviceCreateInput
        {
            Name = "Ghost",
            Icon = null,
            Color = null,
            NamingTemplate = null,
            ImportOnPurchase = false,
        };

        // Act
        var result = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
        scenario.DbContext.Devices.Any(d => d.Name == "Ghost").ShouldBeFalse();
    }

    [Fact]
    public async Task Create_SetsOwnerReferenceOnDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var (service, currentUser) = CreateService(scenario);
        var request = new DeviceCreateInput
        {
            Name = "Tablet",
            Icon = null,
            Color = null,
            NamingTemplate = null,
            ImportOnPurchase = false,
        };

        // Act
        var result = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        var stored = scenario.DbContext.Devices.Single(d => d.Id == result.Device.Id);
        stored.Owner.ShouldNotBeNull();
        stored.Owner.Id.ShouldBe(currentUser.Id);
    }
}