namespace MyMusic.Common.Metadata;

public class CoverArtMetadata
{
    public string? Normal { get; set; }

    public string? Big { get; set; }

    public string? Small { get; set; }

    public string? Biggest => Big ?? Normal ?? Small;

    public string? Smallest => Small ?? Normal ?? Big;

    public CoverArtMetadata()
    { }

    public CoverArtMetadata(string? small, string? normal, string? big)
    {
        Small = small;
        Normal = normal;
        Big = big;
    }

    public CoverArtMetadata Clone()
    {
        return new CoverArtMetadata(small: Small, normal: Normal, big: Big);
    }
}