using MyMusic.Common.Entities;
using MyMusic.Common.Services.Devices;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Devices;

public class DeviceFilterValuesServiceSpecs
{
    private static DeviceFilterValuesService CreateService(Scenario scenario) => new(scenario.DbContext);

    [Fact]
    public async Task Get_Name_ReturnsDistinctOwnerScopedNames()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        scenario.CreateDevice("Phone");
        scenario.CreateDevice("Tablet");
        scenario.CreateDevice("OtherPhone", ownerId: otherUser.Id);

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "name", null, 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldContain("Phone");
        result.Values.ShouldContain("Tablet");
        result.Values.ShouldNotContain("OtherPhone");
    }

    [Fact]
    public async Task Get_Icon_ReturnsDistinctNonNullIcons()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("D1");
        scenario.DbContext.Devices.Single(d => d.Name == "D1").Icon = "phone";
        scenario.CreateDevice("D2");
        scenario.DbContext.Devices.Single(d => d.Name == "D2").Icon = "phone"; // duplicate
        scenario.CreateDevice("D3"); // no icon
        scenario.DbContext.SaveChanges();

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "icon", null, 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(1);
        result.Values.ShouldContain("phone");
    }

    [Fact]
    public async Task Get_Color_ReturnsDistinctNonNullColors()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("D1");
        scenario.DbContext.Devices.Single(d => d.Name == "D1").Color = "#fff";
        scenario.CreateDevice("D2");
        scenario.DbContext.Devices.Single(d => d.Name == "D2").Color = "#000";
        scenario.CreateDevice("D3"); // no color

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "color", null, 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldContain("#fff");
        result.Values.ShouldContain("#000");
    }

    [Fact]
    public async Task Get_UnknownField_ReturnsEmpty()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Phone");

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "unknownField", null, 15, CancellationToken.None);

        // Assert
        result.Values.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_WithSearch_FiltersCaseInsensitiveContains()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Galaxy Phone");
        scenario.CreateDevice("iPad Tablet");
        scenario.CreateDevice("Galaxy Tablet");

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "name", "galaxy", 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
        result.Values.ShouldContain("Galaxy Phone");
        result.Values.ShouldContain("Galaxy Tablet");
        result.Values.ShouldNotContain("iPad Tablet");
    }

    [Fact]
    public async Task Get_Limit_CapsResults()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("A");
        scenario.CreateDevice("B");
        scenario.CreateDevice("C");
        scenario.CreateDevice("D");

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "name", null, 2, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Get_OrdersAscending()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Zeta");
        scenario.CreateDevice("Alpha");
        scenario.CreateDevice("Mid");

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "name", null, 15, CancellationToken.None);

        // Assert
        result.Values.ShouldBe(["Alpha", "Mid", "Zeta"], ignoreOrder: true);
        result.Values[0].ShouldBe("Alpha");
        result.Values[1].ShouldBe("Mid");
        result.Values[2].ShouldBe("Zeta");
    }

    [Fact]
    public async Task Get_OnlyReturnsCurrentUserDevices()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        scenario.CreateDevice("Mine", ownerId: scenario.AdminUser.Id);
        scenario.CreateDevice("Theirs", ownerId: otherUser.Id);

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "name", null, 15, CancellationToken.None);

        // Assert
        result.Values.Count.ShouldBe(1);
        result.Values[0].ShouldBe("Mine");
    }

    [Fact]
    public async Task Get_SearchNull_ReturnsAllMatchingField()
    {
        // Arrange
        var scenario = new Scenario();
        scenario.CreateDevice("Phone");

        // Act
        var result = await CreateService(scenario).GetAsync(scenario.AdminUser.Id, "name", null, 15, CancellationToken.None);

        // Assert
        result.Values.ShouldContain("Phone");
    }
}