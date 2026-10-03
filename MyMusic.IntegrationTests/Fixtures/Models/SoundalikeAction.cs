namespace MyMusic.IntegrationTests.Fixtures.Models;

/// <summary>
/// What happens to a song that is not kept when resolving a group of soundalikes, mirroring the options of the
/// soundalike audit page.
/// </summary>
public enum SoundalikeAction
{
    /// <summary>The song is deleted.</summary>
    Delete,

    /// <summary>The song's metadata is merged into the kept song, then the song is deleted.</summary>
    Merge,

    /// <summary>The song is left untouched, and is no longer reported as a soundalike of the kept song.</summary>
    Ignore,
}
