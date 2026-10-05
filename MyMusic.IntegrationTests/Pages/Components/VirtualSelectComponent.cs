using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// A searchable select whose options dropdown is virtualized (e.g. the artist picker). Rooted at its input.
/// </summary>
public class VirtualSelectComponent(ILocator root) : BaseComponent(root)
{
    // The options dropdown is rendered in a portal, outside the input
    public ILocator Options => Root.Page.GetByRole(AriaRole.Option);

    /// <summary>
    /// Opens the dropdown without searching, listing every item.
    /// </summary>
    public async Task OpenAsync()
    {
        await Root.ClickAsync();
        await Options.First.WaitForAsync();
    }

    /// <summary>
    /// Searches for the option and picks it; the input shows it afterwards.
    /// </summary>
    public async Task SelectAsync(string text)
    {
        await Root.FillAsync(text);
        await Options.Filter(new() { HasTextString = text }).First.ClickAsync();
        await Assertions.Expect(Root).ToHaveValueAsync(text);
    }
}
