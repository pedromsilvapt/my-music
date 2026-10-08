using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// What the naming template field of the device editor shows below its editor: the errors of the template,
/// or the file names it would give to the songs of the device.
/// </summary>
public class NamingPreviewComponent(ILocator root) : BaseComponent(root)
{
    /// <summary>
    /// How many files would be renamed (<c>data-renamed</c>) out of the files of the device (<c>data-total</c>).
    /// </summary>
    public ILocator Summary => Root.GetByTestId("naming-preview-summary");

    /// <summary>
    /// The syntax errors of the template.
    /// </summary>
    public ILocator Errors => Root.GetByTestId("naming-template-error");

    private ILocator RenamedRows => Root.Locator("[data-testid='naming-preview-row'][data-changed='true']");

    /// <summary>
    /// Waits for the preview of the template as it is typed now.
    /// </summary>
    public Task WaitForLoadedAsync() => WaitForAttributeAsync("data-loading", "false", timeout: 15000);

    /// <summary>
    /// The files the template would rename, as pairs of their current and their new path.
    /// </summary>
    public async Task<List<(string CurrentPath, string NewPath)>> GetRenamesAsync()
    {
        var renames = new List<(string, string)>();

        foreach (var row in await RenamedRows.AllAsync())
        {
            var currentPath = await row.GetByTestId("naming-preview-current").InnerTextAsync();
            var newPath = await row.GetByTestId("naming-preview-new").InnerTextAsync();
            renames.Add((currentPath.Trim(), newPath.Trim()));
        }

        return renames;
    }
}
