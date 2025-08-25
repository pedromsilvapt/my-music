namespace MyMusic.Common.Services;

public class UserMusicServiceFactory(MusicDbContext dbContext)
{
    public UserMusicService Create(int userId)
    {
        return new UserMusicService(dbContext, userId);
    }
}