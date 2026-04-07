namespace MyMusic.Common.Entities;

public class SongAcousticFingerprint
{
    public string Checksum { get; set; } = null!;
    public string ChecksumAlgorithm { get; set; } = null!;
    public long OwnerId { get; set; }
    
    public byte[] Fingerprint { get; set; } = null!;
    public double Duration { get; set; }
    
    public double FingerprintLength { get; set; }
    public int FingerprintAlgorithm { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    
    public User Owner { get; set; } = null!;
}
