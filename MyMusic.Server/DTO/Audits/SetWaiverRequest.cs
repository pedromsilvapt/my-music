using System.ComponentModel.DataAnnotations;

namespace MyMusic.Server.DTO.Audits;

public record SetWaiverRequest
{
    public required bool HasWaiver { get; set; }

    [MaxLength(500)] public string? WaiverReason { get; set; }
}