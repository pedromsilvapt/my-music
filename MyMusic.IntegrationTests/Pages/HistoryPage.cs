using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class HistoryPage(IPage page) : BasePage(page, "history")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));
}
