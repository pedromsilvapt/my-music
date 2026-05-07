using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class DevicesFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    [Fact]
    public async Task SeedAsync_CreatesDevices()
    {
        var fixture = new DevicesFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        data.ShouldNotBeEmpty();
        data.Count.ShouldBe(3);

        foreach (var device in data)
        {
            device.Id.ShouldBeGreaterThan(0);
            device.Name.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task SeedAsync_ReturnsDevicesWithExpectedNames()
    {
        var fixture = new DevicesFixture();
        var data = await fixture.SeedAsync(RequestContext, UserId);

        var deviceNames = data.Select(d => d.Name).ToList();
        deviceNames.ShouldContain("Test Device 1");
        deviceNames.ShouldContain("Test Device 2");
        deviceNames.ShouldContain("Test Device 3");
    }
}
