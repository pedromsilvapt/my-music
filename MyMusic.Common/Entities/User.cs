using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class User
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Username { get; set; }

    [MaxLength(256)] public required string Name { get; set; }

    [MaxLength(10)] public string ColorScheme { get; set; } = "auto";

    public double Volume { get; set; } = 1.0;

    public bool IsMuted { get; set; } = false;
}