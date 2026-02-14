using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class Source
{
    public long Id { get; set; }

    [MaxLength(256)] public required string Name { get; set; }

    [MaxLength(256)] public required string Icon { get; set; }

    [MaxLength(1024)] public required string Address { get; set; }

    public required bool IsPaid { get; set; }
}