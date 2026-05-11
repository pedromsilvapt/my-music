using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

/// <summary>
/// Service for deleting users and all their associated data.
/// </summary>
public interface IUserDeleteService
{
    /// <summary>
    /// Deletes a user by ID, including all associated entities.
    /// </summary>
    /// <param name="id">The ID of the user to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deleted user, or null if not found.</returns>
    Task<User?> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
