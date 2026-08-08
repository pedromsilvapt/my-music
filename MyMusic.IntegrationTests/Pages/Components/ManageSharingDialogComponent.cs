using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManageSharingDialogComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task SelectRecipientAsync(string username, string action = "add")
    {
        var recipientRow = Root.Locator($"[data-testid='share-row'][data-share-username='{username}']");
        await recipientRow.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var segmentedControl = recipientRow.Locator("[role='listbox'], .mantine-SegmentedControl-root");
        await segmentedControl.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var actionButton = action.ToLower() switch
        {
            "add" => segmentedControl.GetByText("Add"),
            "remove" => segmentedControl.GetByText("Remove"),
            _ => segmentedControl.GetByText("None"),
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