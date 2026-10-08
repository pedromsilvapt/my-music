using Microsoft.Playwright;
using MyMusic.IntegrationTests.Flows;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The device editor, opened to edit a device or to create one.
/// </summary>
public class DeviceEditorModalComponent(ILocator root) : BaseComponent(root)
{
    public ILocator NameInput => Root.GetByTestId("device-editor-name");

    public ILocator IconSelect => Root.GetByTestId("device-editor-icon");

    public ILocator ColorInput => Root.GetByTestId("device-editor-color");

    public ILocator ImportOnPurchaseSwitch => Root.GetByTestId("device-editor-import-on-purchase");

    public ILocator SubmitButton => Root.GetByTestId("device-editor-submit");

    /// <summary>
    /// The reason the device could not be saved, shown while the dialog stays open.
    /// </summary>
    public ILocator Error => Root.GetByTestId("device-editor-error");

    public ScribanTemplateEditorComponent NamingTemplateEditor => new(Root.GetByTestId("naming-template-editor"));

    public NamingPreviewComponent NamingPreview => new(Root.GetByTestId("naming-template-field"));

    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        // The template editor is loaded on demand
        await Root.GetByTestId("naming-template-editor").Locator(".monaco-editor").First.WaitForAsync(new() { Timeout = 30000 });
        await NamingPreview.WaitForLoadedAsync();
    }

    /// <summary>
    /// Changes the fields of the device: a <see langword="null"/> value leaves that field as it is.
    /// </summary>
    public async Task FillAsync(EditDeviceOptions changes)
    {
        if (changes.Name is not null)
        {
            await NameInput.FillAsync(changes.Name);
        }

        if (changes.Icon is not null)
        {
            await IconSelect.ClickAsync();
            // The options are rendered in a portal, outside the dialog
            await Root.Page.Locator($"[data-testid='device-editor-icon-option'][data-icon='{changes.Icon}']").ClickAsync();
        }

        if (changes.Color is not null)
        {
            await ColorInput.FillAsync(changes.Color);
            await ColorInput.BlurAsync();
        }

        if (changes.ImportOnPurchase is not null
            && await ImportOnPurchaseSwitch.IsCheckedAsync() != changes.ImportOnPurchase)
        {
            // Mantine renders the Switch <input> as visually hidden: its parent is the visible toggle
            await ImportOnPurchaseSwitch.Locator("xpath=..").ClickAsync();
        }

        if (changes.NamingTemplate is not null)
        {
            await SetNamingTemplateAsync(changes.NamingTemplate);
        }
    }

    /// <summary>
    /// Types a naming template, and waits for its preview.
    /// </summary>
    public async Task SetNamingTemplateAsync(string namingTemplate)
    {
        await NamingTemplateEditor.SetValueAsync(namingTemplate);
        // The preview is asked for a moment after the typing stops
        await Assertions.Expect(Root.GetByTestId("naming-template-field"))
            .ToHaveAttributeAsync("data-loading", "true", new() { Timeout = 5000 });
        await NamingPreview.WaitForLoadedAsync();
    }

    /// <summary>
    /// Saves the device, and waits for the dialog to close: it stays open until the server is done.
    /// </summary>
    public async Task SaveAsync()
    {
        await SubmitButton.ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 30000 });
    }

    /// <summary>
    /// Saves expecting the server to refuse, and returns the error the dialog shows.
    /// </summary>
    public async Task<string> SaveRejectedAsync()
    {
        await SubmitButton.ClickAsync();
        await Error.WaitForAsync(new() { Timeout = 15000 });
        return (await Error.InnerTextAsync()).Trim();
    }

    /// <summary>
    /// Closes the dialog without saving.
    /// </summary>
    public async Task CancelAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
