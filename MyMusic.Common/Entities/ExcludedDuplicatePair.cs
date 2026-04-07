namespace MyMusic.Common.Entities;

public class ExcludedDuplicatePair
{
    public long Id { get; set; }
    
    public long SongAId { get; set; }
    public Song SongA { get; set; } = null!;
    
    public long SongBId { get; set; }
    public Song SongB { get; set; } = null!;
    
    public long OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}
