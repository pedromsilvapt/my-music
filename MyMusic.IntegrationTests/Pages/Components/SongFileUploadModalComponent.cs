using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The dialog of the "Upload Song" tool, opened from the tools menu of the edit song modal.
/// </summary>
public class SongFileUploadModalComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task WaitForOpenedAsync()
    {
        await Root.WaitForAsync();
    }

    /// <summary>
    /// Picks the file in the dialog's drop area, as choosing it in the file browser would. Nothing is uploaded yet.
    /// </summary>
    public async Task ChooseFileAsync(SongFile file)
    {
        await Root.GetByTestId("song-file-upload-input").SetInputFilesAsync(new FilePayload
        {
            Name = file.Name,
            MimeType = "application/octet-stream",
            Buffer = file.Content,
        });

        await Assertions.Expect(Root.GetByTestId("song-file-upload-selected")).ToHaveTextAsync(file.Name);
    }

    /// <summary>
    /// Confirms the replacement, and waits for the dialog to close once the server has accepted the file.
    /// </summary>
    public async Task ReplaceAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Replace" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task CancelAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
