using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Entities;

public class Patch
{
    public long Id { get; set; }

    public required string EntityType { get; set; }

    public long EntityId { get; set; }

    public required object Entity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }
}
