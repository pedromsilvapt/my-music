using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Pages;
using Shouldly;

namespace MyMusic.IntegrationTests.Tests;

public class TestUserDisplayTests : IntegrationTestBase
{
    [Fact]
    public async Task Username_ShouldAppearInTopbar()
    {
        var home = new HomePage(Page);

        await home.Topbar.WaitForUsernameAsync(UserName);

        var isVisible = await home.Topbar.Username.IsVisibleAsync();
        isVisible.ShouldBeTrue($"Username '{UserName}' should be visible in the topbar");
    }
}
