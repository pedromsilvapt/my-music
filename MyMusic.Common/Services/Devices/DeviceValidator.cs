using Microsoft.EntityFrameworkCore;
using Scriban;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Thrown when a user already has a device with the requested name: device names are unique per user.
/// </summary>
public class DeviceNameAlreadyExistsException(string message) : ValidationException(message);

/// <summary>
/// The rules a device's name and naming template must follow, shared by the create and update services.
/// </summary>
public static class DeviceValidator
{
    public const int NameMaxLength = 256;
    public const int NamingTemplateMaxLength = 2048;

    /// <summary>
    /// Returns <paramref name="name"/> trimmed.
    /// </summary>
    /// <exception cref="ValidationException">The name is empty or too long.</exception>
    public static string NormalizeName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new ValidationException("Device name cannot be empty");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new ValidationException($"Device name cannot be longer than {NameMaxLength} characters");
        }

        return trimmed;
    }

    /// <summary>
    /// Returns <paramref name="namingTemplate"/>, or <c>null</c> (the server's default template) when it is blank.
    /// </summary>
    /// <exception cref="ValidationException">The template has syntax errors or is too long.</exception>
    public static string? NormalizeNamingTemplate(string? namingTemplate)
    {
        if (string.IsNullOrWhiteSpace(namingTemplate))
        {
            return null;
        }

        if (namingTemplate.Length > NamingTemplateMaxLength)
        {
            throw new ValidationException(
                $"Naming template cannot be longer than {NamingTemplateMaxLength} characters");
        }

        var template = Template.Parse(namingTemplate);
        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages.Select(m => m.ToString()));
            throw new ValidationException($"Naming template is not valid: {errors}");
        }

        return namingTemplate;
    }

    /// <exception cref="DeviceNameAlreadyExistsException">The user already has a device with that name.</exception>
    public static async Task EnsureNameIsFreeAsync(
        MusicDbContext db, long ownerId, string name, CancellationToken cancellationToken)
    {
        if (await db.Devices.AnyAsync(d => d.OwnerId == ownerId && d.Name == name, cancellationToken))
        {
            throw new DeviceNameAlreadyExistsException($"There is already a device named '{name}'");
        }
    }
}
