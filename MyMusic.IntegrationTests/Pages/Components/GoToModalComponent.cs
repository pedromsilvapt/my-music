using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class GoToModalComponent(ILocator root) : BaseComponent(root)
{
    public ILocator SearchInput => Root.GetByRole(AriaRole.Textbox, new() { Name = "Type to search..." });

    // The options dropdown is rendered in a portal, outside the dialog element
    public ILocator Options => Root.Page.GetByRole(AriaRole.Option);

    public ILocator EmptyMessage => Root.Page.GetByText("No items found");

    // Playwright's role engine doesn't account for `inert`, so look for links outside an inert subtree
    public ILocator InteractiveLinks => Options.Locator("a:not([inert] a)");

    public ILocator SelectedOption => Options.And(Root.Page.Locator("[aria-selected='true']"));

    public ILocator GetOption(string text) => Options.Filter(new() { HasTextString = text }).First;

    public async Task WaitForVisibleAsync(int timeout = 5000)
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
    }

    /// <summary>
    /// Whether the options dropdown is rendered below the search input rather than covering it.
    /// </summary>
    public async Task<bool> IsDropdownBelowInputAsync()
    {
        var input = await SearchInput.BoundingBoxAsync();
        var list = await Root.Page.GetByRole(AriaRole.Listbox).BoundingBoxAsync();
        return input is not null && list is not null && list.Y >= input.Y + input.Height;
    }

    /// <summary>
    /// Types the given text into the auto-focused search input.
    /// </summary>
    public async Task SearchAsync(string text)
    {
        await SearchInput.FillAsync(text);
    }

    /// <summary>
    /// Clicks the first option containing the given text; the modal closes afterwards.
    /// </summary>
    public async Task SelectAsync(string text)
    {
        await GetOption(text).ClickAsync();
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Clicks the search input to reveal the options without typing.
    /// </summary>
    public async Task OpenOptionsAsync()
    {
        await SearchInput.ClickAsync();
        await Options.First.WaitForAsync();
    }

    /// <summary>
    /// Presses a key (e.g. "ArrowUp") in the search input.
    /// </summary>
    public async Task PressAsync(string key)
    {
        await SearchInput.PressAsync(key);
    }

    /// <summary>
    /// Returns the song title of the selected option, taken from its "Title - Artists - Album" accessible label.
    /// </summary>
    public async Task<string> GetSelectedTitleAsync()
    {
        var label = await SelectedOption.GetAttributeAsync("aria-label");
        return label!.Split(" - ")[0];
    }

    /// <summary>
    /// Submits the currently selected option with the Enter key; the modal closes afterwards.
    /// </summary>
    public async Task SubmitWithEnterAsync()
    {
        await SearchInput.PressAsync("Enter");
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Moves the mouse over the first option containing the given text.
    /// </summary>
    public async Task HoverAsync(string text)
    {
        await GetOption(text).HoverAsync();
    }

    /// <summary>
    /// Closes the modal with the Escape key; when the options dropdown is open, the first Escape only closes it.
    /// </summary>
    public async Task CloseAsync()
    {
        if (await SearchInput.GetAttributeAsync("data-expanded") is not null)
            await Root.Page.Keyboard.PressAsync("Escape");
        await Root.Page.Keyboard.PressAsync("Escape");
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }
}
