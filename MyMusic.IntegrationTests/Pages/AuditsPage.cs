using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class AuditsPage(IPage page) : BasePage(page, "audits")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));
}
