using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManageSharingDialogComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task WaitForLoadedAsync()
    {
        var content = Root.GetByTestId("manage-sharing");
        await content.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Assertions.Expect(content).ToHaveAttributeAsync("data-loading", "false", new() { Timeout = 10000 });
    }

    public async Task SelectRecipientAsync(string username, ShareAction action = ShareAction.Add)
    {
        var recipientRow = Root.Locator($"[data-testid='share-row'][data-share-username='{username}']");
        await recipientRow.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var segmentedControl = recipientRow.Locator("[role='listbox'], .mantine-SegmentedControl-root");
        await segmentedControl.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var actionButton = action switch
        {
            ShareAction.Add => segmentedControl.GetByText("Add"),
            ShareAction.Remove => segmentedControl.GetByText("Remove"),
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