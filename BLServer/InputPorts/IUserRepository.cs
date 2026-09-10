using Server.Models;

namespace Server.InputPorts;

public interface IUserRepository
{
    public Task AddUserAsync(User user);
    public Task<User?> FindUserAsync(Guid id);
    public Task<User?> FindUserByNameAsync(string name);
    public Task UpdateUserAsync(User user);
    public Task DeleteUserAsync(User user);
    public Task<IEnumerable<User>> FindUsersByGameIdAsync(Guid gameId);
    public Task ClearGameAsync(Guid gameId);
}
