namespace MyMusic.Server.DTO.Devices;

public record PreviewDeviceNamingTemplateRequest
{
    /// <summary>The device whose songs are previewed. Without it, the template is only validated.</summary>
    public long? DeviceId { get; init; }

    /// <summary>The song to preview the path of, as if it was added to a device with the template.</summary>
    public long? SongId { get; init; }

    /// <summary>The template to preview. Blank previews the server's default template.</summary>
    public string? NamingTemplate { get; init; }
}
