using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Pages;

public class PurchasesPage(IPage page) : BasePage(page, "purchases")
{
    public CollectionComponent Collection => new(Root.GetByTestId("collection"));
}
