using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AuditsPage(IPage page) : BasePage(page)
{
    public CollectionComponent Collection => new(Page.GetByTestId("collection"));
}
