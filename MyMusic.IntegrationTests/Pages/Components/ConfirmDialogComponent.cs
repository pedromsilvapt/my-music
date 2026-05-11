using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ConfirmDialogComponent(ILocator root) : BaseComponent(root)
{
    /// <summary>
    /// Clicks the confirm button (e.g., "Delete") in the confirmation dialog.
    /// </summary>
    public async Task ConfirmAsync()
    {
        var confirmButton = Root.GetByRole(AriaRole.Button, new() { Name = "Delete" });
        await confirmButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Clicks the cancel button in the confirmation dialog.
    /// </summary>
    public async Task CancelAsync()
    {
        var cancelButton = Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
        await cancelButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Waits for the dialog to become visible.
    /// </summary>
    public async Task WaitForVisibleAsync(int timeout = 5000)
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
    }
}
