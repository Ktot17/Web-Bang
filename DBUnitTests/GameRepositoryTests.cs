using AutoFixture.Xunit2;
using DBComponent;
using Moq;
using DBComponent.Postgres;
using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace DBUnitTests;

public class GameRepositoryTests
{
    private readonly Mock<ServerDbContext> _mockContext = new();
    private readonly Mock<DbSet<Game>> _mockGamesDbSet = new();
    private readonly GameRepository _gameRepository;

    public GameRepositoryTests()
    {
        var gamesData = new List<Game>().AsQueryable();

        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Provider).Returns(gamesData.Provider);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Expression).Returns(gamesData.Expression);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.ElementType).Returns(gamesData.ElementType);
        using var enumerator = gamesData.GetEnumerator();
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        _mockContext.Setup(c => c.Games).Returns(_mockGamesDbSet.Object);

        _gameRepository = new GameRepository(_mockContext.Object);
    }

    [Theory, AutoData]
    public void AddGame_WhenCalled_AddsGameAndSavesChanges(Guid gameId, Guid hostId)
    {
        // Act
        _gameRepository.AddGame(gameId, hostId);

        // Assert
        _mockGamesDbSet.Verify(m => m.Add(It.Is<Game>(l =>
            l.Id == gameId && l.HostId == hostId && !l.IsEnded && l.PlayerCount == 1)), Times.Once);
        _mockContext.Verify(m => m.SaveChanges(), Times.Once);
    }

    [Theory, AutoData]
    public void FindGame_WhenGameExists_ReturnsGame(Game game)
    {
        // Arrange
        var gamesData = new List<Game> { game }.AsQueryable();

        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Provider).Returns(gamesData.Provider);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Expression).Returns(gamesData.Expression);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.ElementType).Returns(gamesData.ElementType);
        using var enumerator = gamesData.GetEnumerator();
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        var result = _gameRepository.FindGame(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal(game.HostId, result.HostId);
        Assert.Equal(game.IsEnded, result.IsEnded);
        Assert.Equal(game.PlayerCount, result.PlayerCount);
        Assert.Equal(game.GameState, result.GameState);
    }

    [Theory, AutoData]
    public void FindGame_WhenGameDoesNotExist_ReturnsNull(Guid gameId)
    {
        // Act
        var result = _gameRepository.FindGame(gameId);

        // Assert
        Assert.Null(result);
    }

    [Theory, AutoData]
    public void DeleteGame_WhenGameExists_RemovesGameAndSavesChanges(Game game)
    {
        // Arrange
        var gamesData = new List<Game> { game }.AsQueryable();

        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Provider).Returns(gamesData.Provider);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Expression).Returns(gamesData.Expression);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.ElementType).Returns(gamesData.ElementType);
        using var enumerator = gamesData.GetEnumerator();
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        _gameRepository.DeleteGame(game.Id);

        // Assert
        _mockGamesDbSet.Verify(m => m.Remove(game), Times.Once);
        _mockContext.Verify(m => m.SaveChanges(), Times.Once);
    }

    [Theory, AutoData]
    public void DeleteGame_WhenGameDoesNotExist_ThrowsException(Guid gameId)
    {
        // Act & Assert
        Assert.Throws<EntityNotFoundException>(() => _gameRepository.DeleteGame(gameId));
    }

    [Theory, AutoData]
    public void GetIrrelevantGames_WhenCalled_ReturnsIrrelevantGames(Game oldGame, Game newGame)
    {
        // Arrange
        var timeSpan = TimeSpan.FromHours(1);
        oldGame.LastUpdated = DateTimeOffset.UtcNow.AddHours(-2).DateTime;
        newGame.LastUpdated = DateTimeOffset.UtcNow.DateTime;

        var gamesData = new List<Game> { oldGame, newGame }.AsQueryable();

        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Provider).Returns(gamesData.Provider);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Expression).Returns(gamesData.Expression);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.ElementType).Returns(gamesData.ElementType);
        using var enumerator = gamesData.GetEnumerator();
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        var result = _gameRepository.GetIrrelevantGames(timeSpan).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(oldGame.Id, result[0].Id);
        Assert.Equal(oldGame.HostId, result[0].HostId);
        Assert.Equal(oldGame.IsEnded, result[0].IsEnded);
        Assert.Equal(oldGame.PlayerCount, result[0].PlayerCount);
        Assert.Equal(oldGame.GameState, result[0].GameState);
    }

    [Theory, AutoData]
    public void UpdateGame_WhenGameExists_UpdatesGameAndSavesChanges(Game game, string gameState, int playerCount)
    {
        // Arrange
        var gamesData = new List<Game> { game }.AsQueryable();

        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Provider).Returns(gamesData.Provider);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.Expression).Returns(gamesData.Expression);
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.ElementType).Returns(gamesData.ElementType);
        using var enumerator = gamesData.GetEnumerator();
        _mockGamesDbSet.As<IQueryable<Game>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        // Act
        _gameRepository.UpdateGame(game.Id, gameState, game.IsEnded, playerCount);

        // Assert
        _mockGamesDbSet.Verify(m => m.Update(It.Is<Game>(l =>
            l.Id == game.Id &&
            l.GameState == gameState &&
            l.IsEnded == game.IsEnded &&
            l.PlayerCount == playerCount)), Times.Once);
        _mockContext.Verify(m => m.SaveChanges(), Times.Once);
    }

    [Theory, AutoData]
    public void UpdateGame_WhenGameDoesNotExist_ThrowsException(Game game, string gameState, int playerCount)
    {
        // Act & Assert
        Assert.Throws<EntityNotFoundException>(() =>
            _gameRepository.UpdateGame(game.Id, gameState, game.IsEnded, playerCount));
    }
}
