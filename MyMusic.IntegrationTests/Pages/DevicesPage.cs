using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class DevicesPage(IPage page) : BasePage(page, "devices")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));
}
