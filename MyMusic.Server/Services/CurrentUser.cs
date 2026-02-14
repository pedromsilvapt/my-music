using MyMusic.Common;
using MyMusic.Common.Services;

namespace MyMusic.Server.Services;

public class HttpCurrentUser(IHttpContextAccessor httpContextAccessor, MusicDbContext db) : ICurrentUser
{
    private readonly Lazy<long> _id = new(() => GetUserId(httpContextAccessor, db));

    public long Id => _id.Value;

    private static long GetUserId(IHttpContextAccessor httpContextAccessor, MusicDbContext db)
    {
        long id = 1;

        var envId = Environment.GetEnvironmentVariable("MYMUSIC_USER_ID");

        if (!string.IsNullOrEmpty(envId))
        {
            id = long.Parse(envId);
        }

        var envHeaderIdEnabled = Environment.GetEnvironmentVariable("MYMUSIC_HEADER_ID_ENABLED") is "1" or "true";
        var headerId = httpContextAccessor.HttpContext?.Request.Headers["X-MyMusic-UserId"];
        if (envHeaderIdEnabled && !string.IsNullOrEmpty(headerId))
        {
            id = long.Parse(headerId!);
        }

        var headerName = (string?)httpContextAccessor.HttpContext?.Request.Headers["X-MyMusic-UserName"];
        if (envHeaderIdEnabled && !string.IsNullOrEmpty(headerName))
        {
            id = db.Users.FirstOrDefault(u => u.Username == headerName)?.Id ?? -1;
        }

        return id;
    }
}