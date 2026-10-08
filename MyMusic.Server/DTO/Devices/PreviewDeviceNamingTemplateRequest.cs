namespace MyMusic.Server.DTO.Devices;

public record PreviewDeviceNamingTemplateRequest
{
    /// <summary>The device whose songs are previewed. Without it, the template is only validated.</summary>
    public long? DeviceId { get; init; }

    /// <summary>The template to preview. Blank previews the server's default template.</summary>
    public string? NamingTemplate { get; init; }
}
