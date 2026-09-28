using System.IO.Hashing;
using System.Text;

namespace MyMusic.Common.Services;

/// <summary>
///     What an <see cref="AdvisoryLockKey"/> protects. Each scope maps to its own advisory lock class id, so keys of
///     different scopes never collide with each other.
/// </summary>
public enum AdvisoryLockScope
{
    /// <summary>A song's content, identified by its checksum algorithm and checksum.</summary>
    SongChecksum = 1,

    /// <summary>An existing song, identified by its id.</summary>
    Song = 2,

    /// <summary>An artist, identified by its name. Artist names are not unique, so no DB constraint can protect them.</summary>
    Artist = 3,

    /// <summary>An album, identified by its album artist name and album name.</summary>
    Album = 4,
}

/// <summary>
///     Identifies an exclusive lock taken through <see cref="IAdvisoryLockService"/>. Mirrors PostgreSQL's two-key
///     advisory lock form (<c>pg_advisory_xact_lock(int4, int4)</c>): the class id encodes the scope and the object id
///     is a hash of the owner and the locked names. Hash collisions only serialize unrelated work; they never allow two
///     holders of the same key.
/// </summary>
public readonly record struct AdvisoryLockKey(int ClassId, int ObjectId) : IComparable<AdvisoryLockKey>
{
    /// <summary>
    ///     Keeps our class ids clear of advisory locks taken by other tools on the same database ("MM" in ASCII).
    /// </summary>
    private const int ClassIdBase = 0x4D4D0000;

    /// <summary>
    ///     Creates the key for a named resource owned by <paramref name="ownerId"/>. Locks are always per owner, so
    ///     different users never contend with each other.
    /// </summary>
    public static AdvisoryLockKey Create(AdvisoryLockScope scope, long ownerId, params ReadOnlySpan<string> parts)
    {
        var text = new StringBuilder().Append(ownerId);

        foreach (var part in parts)
        {
            // The separator keeps ("ab", "c") and ("a", "bc") apart
            text.Append('\0').Append(part);
        }

        var hash = XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(text.ToString()));

        return new AdvisoryLockKey(ClassIdBase + (int)scope, unchecked((int)hash));
    }

    /// <summary>
    ///     Removes duplicates and sorts the keys into the canonical acquisition order. Every caller that takes several
    ///     locks acquires them in this order, so two callers can never wait on each other in a cycle.
    /// </summary>
    public static IReadOnlyList<AdvisoryLockKey> Normalize(IEnumerable<AdvisoryLockKey> keys) =>
        keys.Distinct().Order().ToList();

    public int CompareTo(AdvisoryLockKey other) =>
        ClassId != other.ClassId ? ClassId.CompareTo(other.ClassId) : ObjectId.CompareTo(other.ObjectId);
}
