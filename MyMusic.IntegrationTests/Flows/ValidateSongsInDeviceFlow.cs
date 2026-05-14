using Microsoft.Playwright;
using MyMusic.IntegrationTests.Models;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

public class ValidateSongsInDeviceFlow : IFlow
{
    private readonly string _songTitleToOpen;
    private readonly string _deviceName;
    private readonly IReadOnlyList<SongDeviceValidation> _validations;

    public ValidateSongsInDeviceFlow(
        string songTitleToOpen,
        string deviceName,
        IReadOnlyList<SongDeviceValidation> validations)
    {
        _songTitleToOpen = songTitleToOpen;
        _deviceName = deviceName;
        _validations = validations;
    }

    public ValidateSongsInDeviceFlow(
        string songTitleToOpen,
        string deviceName,
        string? expectedPath = null,
        string? expectedSyncAction = null,
        bool shouldExist = true) : this(
        songTitleToOpen,
        deviceName,
        [new SongDeviceValidation
        {
            SongTitle = songTitleToOpen,
            ExpectedPath = expectedPath,
            ExpectedSyncAction = expectedSyncAction,
            ShouldExist = shouldExist
        }])
    {
    }

    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = await new OpenSongDetailsFlow(_songTitleToOpen).ExecuteAsync(page);

        var manageDevicesButton = page.GetByRole(AriaRole.Button, new() { Name = "Manage Devices" });
        await manageDevicesButton.ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.WaitForAsync();

        var manageDevicesDialog = new ManageDevicesDialogComponent(dialog);
        await manageDevicesDialog.ExpandDeviceAsync(_deviceName);
        await manageDevicesDialog.ValidateSongsAsync(_validations);

        await manageDevicesDialog.CancelAsync();
    }
}
