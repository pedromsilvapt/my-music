using MyMusic.IntegrationTests.Base;
using MyMusic.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace MyMusic.IntegrationTests.Tests.Fixtures;

public class DevicesFixtureTests(ITestOutputHelper output) : IntegrationTestBase(output)
{
    protected override bool NavigateOnInitialize => false;

    // Scenario: Seeding the sample devices creates every one of them
    //   Given a new user without any devices
    //   When the sample devices are seeded
    //   Then all 3 sample devices are created, each with an id and a name
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

    // Scenario: The seeded sample devices have their expected names
    //   Given a new user without any devices
    //   When the sample devices are seeded
    //   Then the created devices include "Test Device 1", "Test Device 2" and "Test Device 3"
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
