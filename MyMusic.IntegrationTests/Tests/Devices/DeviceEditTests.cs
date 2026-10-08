using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using MyMusic.IntegrationTests.Flows;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests.Devices;

/// <summary>
/// Integration tests for the device editor: editing a device from its details page and creating one from the
/// devices page.
/// </summary>
public class DeviceEditTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    private readonly DevicesFixture _devices = new();

    // Scenario: The options of a device edited on the web show up on its details page
    //   Given a device
    //   When the user changes its name, type, color, import on purchase and naming template from its details page
    //   Then the details page shows the new name and options right away
    //   And the device is listed with its new name
    [Fact]
    public async Task EditDevice_EveryOptionChanged_ShouldShowThemOnTheDetailsPage()
    {
        // Setup: a device
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);
        var namingTemplate = "{{ year }}/{{ title }}{{ extension }}";

        // Action: change every option of the device in its editor
        var details = await new EditDeviceFlow(device.Name, new(
            Name: "Kitchen Tablet",
            Icon: "IconDeviceTablet",
            Color: "#12b886",
            ImportOnPurchase: true,
            NamingTemplate: namingTemplate)).ExecuteAsync(Page);

        // Assert: the details page should show the new options without a reload
        var expected = new ValidateDeviceOptions(
            Icon: "IconDeviceTablet",
            Color: "#12b886",
            ImportOnPurchase: true,
            NamingTemplate: namingTemplate,
            UsesDefaultNamingTemplate: false);
        await new ValidateDeviceDetailsFlow("Kitchen Tablet", expected, details).ExecuteAsync(Page);

        // Assert: the device should be found by its new name in the devices list, with the same options
        await new ValidateDeviceDetailsFlow("Kitchen Tablet", expected).ExecuteAsync(Page);
    }

    // Scenario: A naming template with syntax errors cannot be saved
    //   Given a device
    //   When the user types a naming template with a syntax error in the device editor
    //   Then the editor shows the error and no preview
    //   And the device cannot be saved
    //   And the device keeps using the default naming template
    [Fact]
    public async Task EditDevice_NamingTemplateWithSyntaxError_ShouldShowTheErrorAndNotBeSaved()
    {
        // Setup: a device with the default naming template
        var device = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[0]);

        // Action & Assert: an "if" that is never closed should be reported, and block saving
        await new ValidateNamingTemplateRejectedFlow(device.Name, "{{ if year }}{{ title }}{{ extension }}")
            .ExecuteAsync(Page);

        // Assert: the device should be left as it was
        await new ValidateDeviceDetailsFlow(device.Name, new(UsesDefaultNamingTemplate: true)).ExecuteAsync(Page);
    }

    // Scenario: A device cannot take the name of another device
    //   Given two devices
    //   When the user renames one of them to the name of the other
    //   Then the editor stays open, saying there is already a device with that name
    //   And both devices keep their names
    [Fact]
    public async Task EditDevice_RenamedToTheNameOfAnotherDevice_ShouldBeRejected()
    {
        // Setup: two devices
        var devices = await _devices.SeedAsync(RequestContext, UserId, DevicesFixture.DefaultDevices[..2]);

        // Action: rename the first device to the second one's name
        var error = await new EditRejectedDeviceFlow(devices[0].Name, new(Name: devices[1].Name)).ExecuteAsync(Page);

        // Assert: the editor should explain why the device was not saved
        error.ShouldContain($"already a device named '{devices[1].Name}'");

        // Assert: both devices should still be found by their names
        await new ValidateDeviceDetailsFlow(devices[0].Name, new(Icon: devices[0].Icon)).ExecuteAsync(Page);
        await new ValidateDeviceDetailsFlow(devices[1].Name, new(Icon: devices[1].Icon)).ExecuteAsync(Page);
    }

    // Scenario: A device created on the web is shown with the options it was given
    //   Given a user without devices
    //   When the user creates a device from the devices page, choosing its name, type, color and naming template
    //   Then the details page of the new device is shown, with those options
    //   And the device is listed in the devices page
    [Fact]
    public async Task CreateDevice_FromTheDevicesPage_ShouldShowTheNewDeviceWithItsOptions()
    {
        var namingTemplate = "{{ artists_label }}/{{ title }}{{ extension }}";

        // Action: create a device from the toolbar of the devices page
        var details = await new CreateDeviceFlow(new(
            Name: "Car Stereo",
            Icon: "IconUsb",
            Color: "#fd7e14",
            NamingTemplate: namingTemplate)).ExecuteAsync(Page);

        // Assert: the dialog should lead to the details page of the new device
        var expected = new ValidateDeviceOptions(
            Icon: "IconUsb",
            Color: "#fd7e14",
            ImportOnPurchase: false,
            NamingTemplate: namingTemplate,
            UsesDefaultNamingTemplate: false,
            SongCount: 0,
            SessionsCount: 0);
        await new ValidateDeviceDetailsFlow("Car Stereo", expected, details).ExecuteAsync(Page);

        // Assert: the device should be listed in the devices page
        await new ValidateDeviceDetailsFlow("Car Stereo", expected).ExecuteAsync(Page);
    }
}
