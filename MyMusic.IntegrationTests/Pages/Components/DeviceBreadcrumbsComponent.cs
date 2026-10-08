using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The breadcrumbs of the pages of a device: Devices, the device, and the pages below it.
/// </summary>
public class DeviceBreadcrumbsComponent(ILocator root) : BaseComponent(root)
{
    /// <summary>
    /// The page being shown, the last of the breadcrumbs.
    /// </summary>
    public ILocator Current => Root.GetByTestId("device-breadcrumb-current");

    /// <summary>
    /// The link of the breadcrumbs with the given text.
    /// </summary>
    public ILocator GetLink(string text) =>
        Root.GetByTestId("device-breadcrumb-link").Filter(new() { HasText = text });
}
