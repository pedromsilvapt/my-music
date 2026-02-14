namespace MyMusic.Common.Sources;

public class SourceArtwork
{
    public string? Normal { get; set; }

    public string? Big { get; set; }

    public string? Small { get; set; }

    public string? Biggest => Big ?? Normal ?? Small;

    public string? Smallest => Small ?? Normal ?? Big;

    public SourceArtwork() { }

    public SourceArtwork(string? small, string? normal, string? big)
    {
        Small = small;
        Normal = normal;
        Big = big;
    }

    public SourceArtwork Clone()
    {
        return new SourceArtwork(small: Small, normal: Normal, big: Big);
    }
}