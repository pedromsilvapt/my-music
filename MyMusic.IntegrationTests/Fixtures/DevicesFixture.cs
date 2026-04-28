using Microsoft.Playwright;
using MyMusic.IntegrationTests.Fixtures.Models;
using Shouldly;

namespace MyMusic.IntegrationTests.Fixtures;

public class DevicesFixture
{
    public static SampleDevice[] DefaultDevices { get; } =
    [
        new("Test Device 1", "phone", "#FF5733"),
        new("Test Device 2", "laptop", "#33FF57"),
        new("Test Device 3", "tablet", "#5733FF"),
    ];

    public async Task<List<DeviceData>> SeedAsync(IAPIRequestContext api, long userId, SampleDevice[]? devices = null)
    {
        var sampleDevices = devices ?? DefaultDevices;
        var data = new List<DeviceData>();

        foreach (var device in sampleDevices)
        {
            var response = await api.PostAsync("/api/devices", new()
            {
                DataObject = new
                {
                    name = device.Name,
                    icon = device.Icon,
                    color = device.Color,
                },
            });

            response.Ok.ShouldBeTrue($"Failed to create device: {response.Status} {response.StatusText}");

            var json = await response.JsonAsync();
            var id = json?.GetProperty("device").GetProperty("id").GetInt64()
                ?? throw new InvalidOperationException("Failed to get device ID from response");
            var name = json?.GetProperty("device").GetProperty("name").GetString()
                ?? device.Name;
            var icon = json?.GetProperty("device").GetProperty("icon").GetString();
            var color = json?.GetProperty("device").GetProperty("color").GetString();
            string? namingTemplate = null;
            if (json?.GetProperty("device").TryGetProperty("namingTemplate", out var ntProp) == true)
            {
                namingTemplate = ntProp.GetString();
            }

            data.Add(new DeviceData(id, name, icon, color, namingTemplate));
        }

        return data;
    }
}
