using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManagePlaylistsDialogComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    public async Task SelectPlaylistAsync(string playlistName, string action = "add")
    {
        // var playlistRow = Root.GetByTestId("playlist-row").Filter(new()
        // {
        //     HasText = playlistName
        // }).First;

        var playlistRow = Root.Locator($"[data-testid='playlist-row'][data-playlist-name='{playlistName}']");

        await playlistRow.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var segmentedControl = playlistRow.Locator("[role='listbox'], .mantine-SegmentedControl-root");
        await segmentedControl.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var actionButton = action.ToLower() switch
        {
            "add" => segmentedControl.GetByText("Add"),
            "remove" => segmentedControl.GetByText("Remove"),
            _ => segmentedControl.GetByText("None")
        };

        await actionButton.ClickAsync();
    }

    public async Task CreateAndSelectNewPlaylistAsync(string playlistName, string action = "add")
    {
        var newPlaylistInput = Root.GetByPlaceholder("New playlist name");
        await newPlaylistInput.FillAsync(playlistName);

        var addButton = Root.Locator("button").Filter(new()
        {
            Has = Root.Locator("svg")
        }).First;

        await addButton.ClickAsync();

        var newPlaylistRow = Root.GetByTestId("playlist-row").Filter(new()
        {
            HasText = playlistName
        }).First;

        var segmentedControl = newPlaylistRow.Locator("[role='listbox'], .mantine-SegmentedControl-root");
        var actionButton = action.ToLower() switch
        {
            "add" => segmentedControl.GetByText("Add"),
            _ => segmentedControl.GetByText("None")
        };

        await actionButton.ClickAsync();
    }

    public async Task ApplyAsync()
    {
        var applyButton = Root.GetByRole(AriaRole.Button, new() { Name = "Apply" });
        await applyButton.ClickAsync();

        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
    }

    public async Task CancelAsync()
    {
        var cancelButton = Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
        await cancelButton.ClickAsync();

        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
