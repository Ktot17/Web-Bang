using Microsoft.EntityFrameworkCore;
using Server.InputPorts;
using Server.Models;

namespace DBComponent.MySql;

public class UserRepository(ServerDbContext context) : IUserRepository
{
    public void AddUser(User user)
    {
        context.Users.Add(user);
        context.SaveChanges();
    }

    public User? FindUser(Guid id) =>
        context.Users.AsNoTracking().FirstOrDefault(u => u.Id == id);

    public User? FindUserByName(string name) =>
        context.Users.AsNoTracking().FirstOrDefault(u => u.Name == name);

    public void UpdateUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (FindUser(user.Id) is null)
            throw new EntityNotFoundException(typeof(User));
        context.Users.Update(user);
        context.SaveChanges();
    }

    public void DeleteUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (FindUser(user.Id) is null)
            throw new EntityNotFoundException(typeof(User));
        context.Users.Remove(user);
        context.SaveChanges();
    }

    public IEnumerable<User> FindUsersByGameId(Guid gameId) =>
        context.Users.Where(u => u.GameId == gameId).AsNoTracking();

    public void ClearGame(Guid gameId)
    {
        context.Users.Where(u => u.GameId == gameId).AsNoTracking().
            ExecuteUpdate(set => set.SetProperty(u => u.GameId, (Guid?)null));
        context.SaveChanges();
    }

    public async Task AddUserAsync(User user)
    {
        await context.Users.AddAsync(user).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<User?> FindUserAsync(Guid id) =>
        await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id).ConfigureAwait(false);

    public async Task<User?> FindUserByNameAsync(string name) =>
        await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Name == name).ConfigureAwait(false);

    public Task UpdateUserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return UpdateUserAsyncInner(user);
    }

    public Task DeleteUserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return DeleteUserAsyncInner(user);
    }

    public async Task<IEnumerable<User>> FindUsersByGameIdAsync(Guid gameId) =>
        await context.Users.Where(u => u.GameId == gameId).AsNoTracking().ToListAsync().ConfigureAwait(false);

    public async Task ClearGameAsync(Guid gameId)
    {
        await context.Users.Where(u => u.GameId == gameId).AsNoTracking().
            ExecuteUpdateAsync(set => set.SetProperty(u => u.GameId, (Guid?)null)).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task UpdateUserAsyncInner(User user)
    {
        if (await FindUserAsync(user.Id).ConfigureAwait(false) is null)
            throw new EntityNotFoundException(typeof(User));
        context.Users.Update(user);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task DeleteUserAsyncInner(User user)
    {
        if (await FindUserAsync(user.Id).ConfigureAwait(false) is null)
            throw new EntityNotFoundException(typeof(User));
        context.Users.Remove(user);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
