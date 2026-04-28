using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public interface IUserDeleteService
{
    Task<User?> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
