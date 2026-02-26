using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using Shouldly;

namespace MyMusic.Common.Tests.Seeding;

public class SeedServiceSpecs
{
    [Fact]
    public async Task SeedAsync_NoSeedPath_DoesNothing()
    {
        var scenario = new Scenario();
        var seedService = scenario.CreateSeedService(seedPath: null);

        await seedService.SeedAsync();

        scenario.DbContext.Users.Count().ShouldBe(1);
        scenario.DbContext.Sources.Count().ShouldBe(0);
    }

    [Fact]
    public async Task SeedAsync_FileNotFound_DoesNothing()
    {
        var scenario = new Scenario();
        var seedService = scenario.CreateSeedService(seedPath: "/nonexistent/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Users.Count().ShouldBe(1);
    }

    [Fact]
    public async Task SeedAsync_NewUsers_CreatesUsers()
    {
        var scenario = new Scenario();
        var seedJson = """{"users":[{"username":"testuser","name":"Test User"},{"username":"another","name":"Another User"}]}""";
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Users.Count().ShouldBe(3);
        var users = await scenario.DbContext.Users.Where(u => u.Username != "admin").ToListAsync();
        users.ShouldContain(u => u.Username == "testuser" && u.Name == "Test User");
        users.ShouldContain(u => u.Username == "another" && u.Name == "Another User");
    }

    [Fact]
    public async Task SeedAsync_UsersWithDevices_CreatesUsersAndDevices()
    {
        var scenario = new Scenario();
        var seedJson = """
            {
                "users": [{
                    "username": "testuser",
                    "name": "Test User",
                    "devices": [
                        {"name": "iPhone", "icon": "phone", "color": "#FF0000"},
                        {"name": "iPad", "icon": "tablet", "namingTemplate": "{{title}}.mp3"}
                    ]
                }]
            }
            """;
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        var user = await scenario.DbContext.Users
            .FirstOrDefaultAsync(u => u.Username == "testuser");

        user.ShouldNotBeNull();
        user.Name.ShouldBe("Test User");

        var devices = await scenario.DbContext.Devices
            .Where(d => d.OwnerId == user.Id)
            .ToListAsync();
        devices.Count.ShouldBe(2);
        devices.ShouldContain(d => d.Name == "iPhone" && d.Icon == "phone" && d.Color == "#FF0000");
        devices.ShouldContain(d => d.Name == "iPad" && d.Icon == "tablet" && d.NamingTemplate == "{{title}}.mp3");
    }

    [Fact]
    public async Task SeedAsync_NewSources_CreatesSources()
    {
        var scenario = new Scenario();
        var seedJson = """
            {
                "sources": [
                    {"name": "Spotify", "icon": "spotify", "address": "https://api.spotify.com", "isPaid": true},
                    {"name": "YouTube", "icon": "youtube", "address": "https://youtube.com", "isPaid": false}
                ]
            }
            """;
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Sources.Count().ShouldBe(2);
        var sources = await scenario.DbContext.Sources.ToListAsync();
        sources.ShouldContain(s => s.Name == "Spotify" && s.Icon == "spotify" && s.IsPaid);
        sources.ShouldContain(s => s.Name == "YouTube" && s.Icon == "youtube" && !s.IsPaid);
    }

    [Fact]
    public async Task SeedAsync_UserWithId_UpdatesExistingUser()
    {
        var scenario = new Scenario();
        var existingUser = scenario.AdminUser;
        var seedJson = $$"""{"users":[{"id":{{existingUser.Id}},"username":"admin","name":"Updated Name"}]}""";
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Users.Count().ShouldBe(1);
        var user = await scenario.DbContext.Users.FindAsync(existingUser.Id);
        user.ShouldNotBeNull();
        user.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task SeedAsync_UserByUsername_UpdatesExistingUser()
    {
        var scenario = new Scenario();
        var seedJson = """{"users":[{"username":"admin","name":"Updated Admin"}]}""";
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Users.Count().ShouldBe(1);
        var user = await scenario.DbContext.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        user.ShouldNotBeNull();
        user.Name.ShouldBe("Updated Admin");
    }

    [Fact]
    public async Task SeedAsync_DeviceByOwnerAndName_UpdatesExisting()
    {
        var scenario = new Scenario();
        var existingDevice = new Device
        {
            Name = "iPhone",
            OwnerId = scenario.AdminUser.Id,
            Owner = null!,
            Icon = "old-icon",
            Color = "old-color"
        };
        scenario.DbContext.Add(existingDevice);
        await scenario.DbContext.SaveChangesAsync();

        var seedJson = """
            {
                "users": [{
                    "username": "admin",
                    "name": "Administrator",
                    "devices": [{"name": "iPhone", "icon": "new-icon", "color": "new-color"}]
                }]
            }
            """;
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Devices.Count().ShouldBe(1);
        var device = await scenario.DbContext.Devices.FirstOrDefaultAsync(d => d.Name == "iPhone");
        device.ShouldNotBeNull();
        device.Icon.ShouldBe("new-icon");
        device.Color.ShouldBe("new-color");
    }

    [Fact]
    public async Task SeedAsync_SourceWithId_UpdatesExisting()
    {
        var scenario = new Scenario();
        var existingSource = new Source
        {
            Name = "Spotify",
            Icon = "old-icon",
            Address = "old-address",
            IsPaid = false
        };
        scenario.DbContext.Add(existingSource);
        await scenario.DbContext.SaveChangesAsync();

        var seedJson = $$"""
            {
                "sources": [{
                    "id": {{existingSource.Id}},
                    "name": "Spotify",
                    "icon": "new-icon",
                    "address": "new-address",
                    "isPaid": true
                }]
            }
            """;
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Sources.Count().ShouldBe(1);
        var source = await scenario.DbContext.Sources.FindAsync(existingSource.Id);
        source.ShouldNotBeNull();
        source.Icon.ShouldBe("new-icon");
        source.Address.ShouldBe("new-address");
        source.IsPaid.ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_SourceByName_UpdatesExisting()
    {
        var scenario = new Scenario();
        var existingSource = new Source
        {
            Name = "Spotify",
            Icon = "old-icon",
            Address = "old-address",
            IsPaid = false
        };
        scenario.DbContext.Add(existingSource);
        await scenario.DbContext.SaveChangesAsync();

        var seedJson = """
            {
                "sources": [{
                    "name": "Spotify",
                    "icon": "new-icon",
                    "address": "new-address",
                    "isPaid": true
                }]
            }
            """;
        scenario.FileSystem.File.WriteAllText("/seed.json", seedJson);
        var seedService = scenario.CreateSeedService(seedPath: "/seed.json");

        await seedService.SeedAsync();

        scenario.DbContext.Sources.Count().ShouldBe(1);
        var source = await scenario.DbContext.Sources.FirstOrDefaultAsync(s => s.Name == "Spotify");
        source.ShouldNotBeNull();
        source.Icon.ShouldBe("new-icon");
        source.Address.ShouldBe("new-address");
        source.IsPaid.ShouldBeTrue();
    }
}