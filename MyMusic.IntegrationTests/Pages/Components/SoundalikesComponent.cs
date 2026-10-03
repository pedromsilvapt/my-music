using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The list of soundalike groups shown by the soundalike audit rule's page.
/// </summary>
public class SoundalikesComponent(ILocator root) : BaseComponent(root)
{
    public async Task WaitForLoadedAsync()
    {
        await Root.WaitForAsync(new() { Timeout = 10000 });
        await WaitForAttributeAsync("data-loading", "false");
    }

    /// <summary>
    /// The group holding the song with the given title.
    /// </summary>
    public SoundalikeGroupComponent GroupWithSong(string title) =>
        new(Root.GetByTestId("soundalike-group").Filter(new() { HasText = title }));
}
