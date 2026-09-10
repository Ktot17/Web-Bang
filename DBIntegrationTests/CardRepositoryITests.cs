using AutoFixture.Xunit2;
using BLComponent;
using DBComponent.Postgres;
using Microsoft.EntityFrameworkCore;

namespace DBIntegrationTests;

public class CardRepositoryTests : IAsyncLifetime
{
    private readonly ServerDbContext _db;
    private readonly CardRepository _cardRepository;

    public CardRepositoryTests()
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
        _cardRepository = new CardRepository(_db);
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
    public async Task GetAll_WhenReaderReturnsData_ReturnsListOfCards(
        CardName card1Name, CardSuit card1Suit, CardRank card1Rank,
        CardName card2Name, CardSuit card2Suit, CardRank card2Rank
        )
    {
        // Arrange
        var card1 = new CardDb((int)card1Name, (int)card1Suit, (int)card1Rank);
        var card2 = new CardDb((int)card2Name, (int)card2Suit, (int)card2Rank);

        await _db.Database.ExecuteSqlAsync(
            $"INSERT INTO decks.classicdeck (name, suit, rank) VALUES ({card1.Name}, {card1.Suit}, {card1.Rank})"
        );
        await _db.Database.ExecuteSqlAsync(
            $"INSERT INTO decks.classicdeck (name, suit, rank) VALUES ({card2.Name}, {card2.Suit}, {card2.Rank})"
        );

        // Act
        var result = _cardRepository.GetAll.ToList();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.Equal((CardName)card1.Name, result[0].Name);
        Assert.Equal((CardSuit)card1.Suit, result[0].Suit);
        Assert.Equal((CardRank)card1.Rank, result[0].Rank);

        Assert.Equal((CardName)card2.Name, result[1].Name);
        Assert.Equal((CardSuit)card2.Suit, result[1].Suit);
        Assert.Equal((CardRank)card2.Rank, result[1].Rank);
    }

    [Fact]
    public void GetAll_WhenReaderReturnsNoData_ReturnsEmptyList()
    {
        // Act
        var result = _cardRepository.GetAll;

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
