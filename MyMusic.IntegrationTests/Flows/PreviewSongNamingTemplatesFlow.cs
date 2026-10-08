using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// A template to try in the "Test Naming Template" tool: the naming template of a device, picked from the starter
/// templates, or a template typed by hand.
/// </summary>
public record SongNamingTemplateInput(string? StarterDevice = null, string? Template = null);

/// <summary>
/// Opens a song's edit modal and tries each template, in order, in the "Test Naming Template" tool. Returns, for
/// each one, the path the tool previews for the song, or <see langword="null"/> when it shows the syntax errors of
/// the template instead. The tool is closed at the end.
/// </summary>
public class PreviewSongNamingTemplatesFlow(string songTitle, params SongNamingTemplateInput[] inputs)
    : IFlow<List<string?>>
{
    public async Task<List<string?>> ExecuteAsync(IPage page)
    {
        // Open the tool from the song's edit modal
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        var editModal = await songDetails.OpenEditModalAsync();
        var modal = await editModal.OpenTestNamingTemplateAsync();

        var paths = new List<string?>();

        foreach (var input in inputs)
        {
            if (input.StarterDevice is not null)
            {
                await modal.PickStarterAsync(input.StarterDevice);
            }

            if (input.Template is not null)
            {
                await modal.SetNamingTemplateAsync(input.Template);
            }

            if (await modal.NamingPreview.Errors.CountAsync() > 0)
            {
                // A template that cannot be used is marked in the editor, and previews no path
                await modal.NamingTemplateEditor.WaitForErrorCountAsync(await modal.NamingPreview.Errors.CountAsync());
                await Assertions.Expect(modal.NamingPreview.SongPath).ToHaveCountAsync(0);
                paths.Add(null);
                continue;
            }

            await Assertions.Expect(modal.NamingPreview.SongPath).ToBeVisibleAsync();
            paths.Add((await modal.NamingPreview.SongPath.InnerTextAsync()).Trim());
        }

        await modal.CloseAsync();

        return paths;
    }
}
