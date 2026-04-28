using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class EditSongModalComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task SetTitleAsync(string title)
    {
        await Root.GetByLabel("Title").FillAsync(title);
    }

    public async Task SetYearAsync(int year)
    {
        await Root.GetByLabel("Year").FillAsync(year.ToString());
    }

    public async Task SetLyricsAsync(string lyrics)
    {
        await Root.GetByLabel("Lyrics").FillAsync(lyrics);
    }

    public async Task SetRatingAsync(int rating)
    {
        var ratingContainer = Root.Locator("input[type='range']");
        await ratingContainer.FillAsync(rating.ToString());
    }

    public async Task SetExplicitAsync(bool isExplicit)
    {
        var explicitSwitch = Root.GetByLabel("Explicit");
        var isChecked = await explicitSwitch.IsCheckedAsync();
        if (isChecked != isExplicit)
        {
            await explicitSwitch.ClickAsync();
        }
    }

    public async Task SaveAsync()
    {
        await Root.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = DefaultTimeout });
    }
}
