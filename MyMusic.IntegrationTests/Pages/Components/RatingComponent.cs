using System.Globalization;
using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// Wraps a star rating input, which allows half stars.
/// </summary>
public class RatingComponent(ILocator root) : BaseComponent(root)
{
    // Every half star is a visually hidden radio input, picked by clicking the label linked to it
    private ILocator GetInput(decimal value) =>
        Root.Locator($"input[type='radio'][value='{value.ToString(CultureInfo.InvariantCulture)}']");

    /// <summary>
    /// Picks the given number of stars (e.g. 3.5).
    /// </summary>
    public async Task SetAsync(decimal value)
    {
        var inputId = await GetInput(value).GetAttributeAsync("id");

        // Half star labels overlap each other, so a pointer click could land on the wrong one
        await Root.Locator($"label[for='{inputId}']").DispatchEventAsync("click");
    }

    /// <summary>
    /// Asserts that the rating shows the given number of stars (0 when unrated).
    /// </summary>
    public async Task ShouldHaveValueAsync(decimal value)
    {
        await Assertions.Expect(GetInput(value)).ToBeCheckedAsync();
    }
}
