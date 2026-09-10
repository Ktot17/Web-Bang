using MongoDB.Driver;
using Server.InputPorts;
using Server.Models;

namespace DBComponent.MongoDb;

public class UserRepository(ServerDbContext context) : IUserRepository
{
    private readonly IMongoCollection<User> _users = context.Users;

    public async Task AddUserAsync(User user) =>
        await _users.InsertOneAsync(user).ConfigureAwait(false);

    public async Task<User?> FindUserAsync(Guid id) =>
        await _users.Find(u => u.Id == id).FirstOrDefaultAsync().ConfigureAwait(false);

    public async Task<User?> FindUserByNameAsync(string name) =>
        await _users.Find(u => u.Name == name).FirstOrDefaultAsync().ConfigureAwait(false);

    public async Task UpdateUserAsync(User user)
    {
        var result = await _users.ReplaceOneAsync(u => u.Id == user.Id, user).ConfigureAwait(false);
        if (result.MatchedCount == 0)
            throw new EntityNotFoundException(typeof(User));
    }

    public async Task DeleteUserAsync(User user)
    {
        var result = await _users.DeleteOneAsync(u => u.Id == user.Id).ConfigureAwait(false);
        if (result.DeletedCount == 0)
            throw new EntityNotFoundException(typeof(User));
    }

    public async Task<IEnumerable<User>> FindUsersByGameIdAsync(Guid gameId) =>
        await _users.Find(u => u.GameId == gameId).ToListAsync().ConfigureAwait(false);

    public async Task ClearGameAsync(Guid gameId)
    {
        var update = Builders<User>.Update.Set(u => u.GameId, null);
        await _users.UpdateManyAsync(u => u.GameId == gameId, update).ConfigureAwait(false);
    }
}
