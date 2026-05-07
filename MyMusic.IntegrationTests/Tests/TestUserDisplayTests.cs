using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Pages;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests;

public class TestUserDisplayTests(ITestOutputHelper output) : IntegrationTestBase(output)
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
