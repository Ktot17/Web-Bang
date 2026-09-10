using BLComponent;
using BLUnitTests;
using DBComponent.Postgres;
using Microsoft.EntityFrameworkCore;
using Moq;
using Serilog;

namespace IntegrationTests;

public class BlIntegrationTests : IAsyncLifetime
{
    private readonly GameManagerBuilder _gameManagerBuilder;
    private readonly ServerDbContext _db;

    public BlIntegrationTests()
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
        var cardRepository = new CardRepository(_db);
        _gameManagerBuilder = new GameManagerBuilder(cardRepository,
            new Mock<ILogger>().Object);
    }

    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        var sqlScript = await File.ReadAllTextAsync(
            Path.Combine(Directory.GetCurrentDirectory(),
                "..", "..", "..", "FillClassicDeck.sql"));
        await _db.Database.ExecuteSqlRawAsync(sqlScript);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.Database.EnsureDeletedAsync();

    [Theory, GameManagerAutoData(playerNamesAmount: 7)]
    public void DeckCreation_ITest(Dictionary<Guid, string> players)
    {
        // Arrange
        var gameManager = _gameManagerBuilder.Build();

        // Act
        gameManager.GameInit(players);

        // Assert
        Assert.Equal(80, gameManager.CardsInDeck.Count);
        Assert.Equal(25, gameManager.CardsInDeck.Count(c => c.Name == CardName.Bang));
        Assert.Equal(6, gameManager.CardsInDeck.Count(c => c.Name == CardName.Beer));
        Assert.Equal(12, gameManager.CardsInDeck.Count(c => c.Name == CardName.Missed));
        Assert.Equal(4, gameManager.CardsInDeck.Count(c => c.Name == CardName.Panic));
        Assert.Equal(2, gameManager.CardsInDeck.Count(c => c.Name == CardName.GeneralStore));
        Assert.Equal(2, gameManager.CardsInDeck.Count(c => c.Name == CardName.Indians));
        Assert.Equal(3, gameManager.CardsInDeck.Count(c => c.Name == CardName.Duel));
        Assert.Equal(4, gameManager.CardsInDeck.Count(c => c.Name == CardName.CatBalou));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.Saloon));
        Assert.Equal(2, gameManager.CardsInDeck.Count(c => c.Name == CardName.Stagecoach));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.WellsFargo));
        Assert.Equal(2, gameManager.CardsInDeck.Count(c => c.Name == CardName.Barrel));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.Scope));
        Assert.Equal(2, gameManager.CardsInDeck.Count(c => c.Name == CardName.Mustang));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.Dynamite));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.BeerBarrel));
        Assert.Equal(2, gameManager.CardsInDeck.Count(c => c.Name == CardName.Jail));
        Assert.Equal(2, gameManager.CardsInDeck.Count(c => c.Name == CardName.Volcanic));
        Assert.Equal(3, gameManager.CardsInDeck.Count(c => c.Name == CardName.Schofield));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.Remington));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.Carabine));
        Assert.Equal(1, gameManager.CardsInDeck.Count(c => c.Name == CardName.Winchester));
    }
}
