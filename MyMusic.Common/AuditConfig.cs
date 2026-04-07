namespace MyMusic.Common;

public class AuditConfig
{
    public int MediumCoverThreshold { get; set; } = 1080;
    public int SmallCoverThreshold { get; set; } = 500;

    public double SoundalikeMatchThreshold { get; set; } = 0.90;
    public double SoundalikeLookupThreshold { get; set; } = 0.25;
}