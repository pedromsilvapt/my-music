using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens the details page of a device through the devices list and asserts what it shows. With
/// <paramref name="details"/>, the page already open is validated instead.
/// </summary>
public class ValidateDeviceDetailsFlow(string deviceName, ValidateDeviceOptions expected, DeviceDetailsPage? details = null) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        details ??= await new OpenDeviceDetailsFlow(deviceName).ExecuteAsync(page);

        await Assertions.Expect(details.Name).ToHaveTextAsync(deviceName);
        await Assertions.Expect(details.Breadcrumbs.Current).ToHaveTextAsync(deviceName);

        if (expected.Icon is not null)
        {
            await Assertions.Expect(details.Icon).ToHaveAttributeAsync("data-icon", expected.Icon);
        }

        if (expected.Color is not null)
        {
            await Assertions.Expect(details.Icon).ToHaveAttributeAsync("data-color", expected.Color);
        }

        if (expected.ImportOnPurchase is not null)
        {
            await Assertions.Expect(details.ImportOnPurchase)
                .ToHaveAttributeAsync("data-enabled", expected.ImportOnPurchase.Value ? "true" : "false");
        }

        if (expected.NamingTemplate is not null)
        {
            await Assertions.Expect(details.NamingTemplate).ToHaveTextAsync(expected.NamingTemplate);
        }

        if (expected.UsesDefaultNamingTemplate is not null)
        {
            await Assertions.Expect(details.NamingTemplate)
                .ToHaveAttributeAsync("data-default", expected.UsesDefaultNamingTemplate.Value ? "true" : "false");
            // The template in use is always shown, even when it is the server's default one
            await Assertions.Expect(details.NamingTemplate).Not.ToBeEmptyAsync();
        }

        if (expected.SongCount is not null)
        {
            await Assertions.Expect(details.SongCount).ToHaveAttributeAsync("data-count", expected.SongCount.Value.ToString());
        }

        if (expected.SessionsCount is not null)
        {
            await Assertions.Expect(details.Sessions.Rows).ToHaveCountAsync(expected.SessionsCount.Value);
        }
    }
}
