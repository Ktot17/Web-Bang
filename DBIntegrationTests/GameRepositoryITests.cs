using AutoFixture.Xunit2;
using DBComponent;
using DBComponent.Postgres;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace DBIntegrationTests;

public class GameRepositoryTests : IAsyncLifetime
{
    private readonly ServerDbContext _db;
    private readonly GameRepository _gameRepository;

    public GameRepositoryTests()
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
        _gameRepository = new GameRepository(_db);
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
    public async Task AddGame_WhenCalled_AddsGameAndSavesChanges(Guid gameId, Guid hostId)
    {
        // Act
        await _gameRepository.AddGameAsync(gameId, hostId);

        // Assert
        var game = await _db.Games.FindAsync(gameId);
        Assert.NotNull(game);
        Assert.Equal(gameId, game.Id);
        Assert.Equal(hostId, game.HostId);
    }

    [Theory, AutoData]
    public async Task FindGame_WhenGameExists_ReturnsGame(Game game)
    {
        // Arrange
        game.GameState = null;
        await _db.Games.AddAsync(game);
        await _db.SaveChangesAsync();

        // Act
        var result = await _gameRepository.FindGameAsync(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal(game.HostId, result.HostId);
        Assert.Equal(game.IsEnded, result.IsEnded);
        Assert.Equal(game.PlayerCount, result.PlayerCount);
        Assert.Equal(game.GameState, result.GameState);
    }

    [Theory, AutoData]
    public async Task FindGame_WhenGameDoesNotExist_ReturnsNull(Guid gameId)
    {
        // Act
        var result = await _gameRepository.FindGameAsync(gameId);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public async Task DeleteGame_WhenGameExists_RemovesGameAndSavesChanges(Game game)
    {
        // Arrange
        game.GameState = null;
        await _db.AddAsync(game);
        await _db.SaveChangesAsync();

        // Act
        await _gameRepository.DeleteGameAsync(game.Id);

        // Assert
        Assert.Null(await _db.Games.FindAsync(game.Id));
    }

    [Theory, AutoData]
    public async Task DeleteGame_WhenGameDoesNotExist_ThrowsException(Guid gameId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(async () => await _gameRepository.DeleteGameAsync(gameId));
    }

    [Theory, AutoData]
    public async Task GetIrrelevantGames_WhenCalled_ReturnsIrrelevantGames(Game oldGame, Game newGame)
    {
        // Arrange
        var timeSpan = TimeSpan.FromHours(1);
        oldGame.LastUpdated = DateTimeOffset.UtcNow.AddHours(-2).DateTime;
        newGame.LastUpdated = DateTimeOffset.UtcNow.DateTime;

        oldGame.GameState = null;
        newGame.GameState = null;

        await _db.Games.AddAsync(oldGame);
        await _db.Games.AddAsync(newGame);
        await _db.SaveChangesAsync();

        // Act
        var result = (await _gameRepository.GetIrrelevantGamesAsync(timeSpan)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(oldGame.Id, result[0].Id);
        Assert.Equal(oldGame.HostId, result[0].HostId);
        Assert.Equal(oldGame.IsEnded, result[0].IsEnded);
        Assert.Equal(oldGame.PlayerCount, result[0].PlayerCount);
        Assert.Equal(oldGame.GameState, result[0].GameState);
    }

    [Theory, AutoData]
    public async Task UpdateGame_WhenGameExists_UpdatesGameAndSavesChanges(Game game, int playerCount)
    {
        // Arrange
        game.GameState = null;
        var trackedGame = await _db.Games.AddAsync(game);
        await _db.SaveChangesAsync();
        trackedGame.State = EntityState.Detached;

        // Act
        await _gameRepository.UpdateGameAsync(game.Id, game.GameState, game.IsEnded, playerCount);

        // Assert
        var updatedGame = await _db.Games.FindAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal(game.Id, updatedGame.Id);
        Assert.Equal(game.GameState, updatedGame.GameState);
        Assert.Equal(playerCount, updatedGame.PlayerCount);
    }

    [Theory, AutoData]
    public async Task UpdateGame_WhenGameDoesNotExist_ThrowsException(Game game, string gameState, int playerCount)
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await _gameRepository.UpdateGameAsync(game.Id, gameState, game.IsEnded, playerCount));
    }
}
