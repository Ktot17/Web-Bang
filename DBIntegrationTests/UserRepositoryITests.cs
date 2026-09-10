using AutoFixture.Xunit2;
using DBComponent;
using DBComponent.Postgres;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace DBIntegrationTests;

public class UserRepositoryTests : IAsyncLifetime
{
    private readonly ServerDbContext _db;
    private readonly UserRepository _userRepository;

    public UserRepositoryTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var connString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")?
                             .Replace("Database=test", $"Database=test_{dbName}") ??
                         $"Host=localhost;Port=5433;Database=test_{dbName};" +
                         $"Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseNpgsql(connString)
            .Options;
        _db = new ServerDbContext(options);
        _userRepository = new UserRepository(_db);
    }

    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
    }

    [Theory, AutoData]
    public async Task AddUser_WhenCalled_AddsUserAndSavesChanges(Guid userId,
        string name, string email, string passwordHash)
    {
        // Act
        await _userRepository.AddUserAsync(new User(userId, name, email, passwordHash, null));

        // Assert
        var user = await _db.Users.FindAsync(userId);
        Assert.NotNull(user);
        Assert.Equal(userId, user.Id);
        Assert.Equal(name, user.Name);
        Assert.Equal(passwordHash, user.PasswordHash);
    }

    [Theory, AutoData]
    public async Task FindUser_WhenUserExists_ReturnsUser(User user)
    {
        // Arrange
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        // Act
        var result = await _userRepository.FindUserAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.PasswordHash, result.PasswordHash);
        Assert.Equal(user.GameId, result.GameId);
    }

    [Theory, AutoData]
    public async Task FindUser_WhenUserDoesNotExist_ReturnsNull(Guid userId)
    {
        // Act
        var result = await _userRepository.FindUserAsync(userId);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public async Task FindUserByName_WhenUserExists_ReturnsUser(User user)
    {
        // Arrange
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        // Act
        var result = await _userRepository.FindUserByNameAsync(user.Name);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.PasswordHash, result.PasswordHash);
        Assert.Equal(user.GameId, result.GameId);
    }

    [Theory, AutoData]
    public async Task FindUserByName_WhenUserDoesNotExist_ReturnsNull(string name)
    {
        // Act
        var result = await _userRepository.FindUserByNameAsync(name);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public async Task UpdateUser_WhenUserExists_UpdatesUserAndSavesChanges(User user, string newName)
    {
        // Arrange
        var trackedUser = await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
        trackedUser.State = EntityState.Detached;
        user.Name = newName;

        // Act
        await _userRepository.UpdateUserAsync(user);

        // Assert
        var updatedUser = await _db.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(user.Id, updatedUser.Id);
        Assert.Equal(newName, updatedUser.Name);
        Assert.Equal(user.PasswordHash, updatedUser.PasswordHash);
    }

    [Theory, AutoData]
    public async Task UpdateUser_WhenUserDoesNotExist_ThrowsException(User user, string newName)
    {
        // Arrange
        user.Name = newName;

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await _userRepository.UpdateUserAsync(user));
    }

    [Theory, AutoData]
    public async Task DeleteUser_WhenUserExists_DeletesUserAndSavesChanges(User user)
    {
        // Arrange
        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();

        // Act
        await _userRepository.DeleteUserAsync(user);

        // Assert
        Assert.Null(await _db.Users.FindAsync(user.Id));
    }

    [Theory, AutoData]
    public async Task DeleteUser_WhenUserDoesNotExist_ThrowsException(User user)
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await _userRepository.DeleteUserAsync(user));
    }

    [Theory, AutoData]
    public async Task FindUsersByGameId_WhenUsersExist_ReturnsUsers(Guid gameId, User user1, User user2, User user3)
    {
        // Arrange
        user1.GameId = gameId;
        user2.GameId = gameId;

        await _db.Users.AddAsync(user1);
        await _db.Users.AddAsync(user2);
        await _db.Users.AddAsync(user3);
        await _db.SaveChangesAsync();

        // Act
        var result = (await _userRepository.FindUsersByGameIdAsync(gameId)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, u => Assert.Equal(gameId, u.GameId));
    }
}
