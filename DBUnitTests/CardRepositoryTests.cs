using AutoFixture.Xunit2;
using Moq;
using BLComponent;
using DBComponent.Postgres;
using Microsoft.EntityFrameworkCore;

namespace DBUnitTests;

public class CardRepositoryTests
{
    private readonly Mock<ServerDbContext> _mockContext = new();
    private readonly Mock<DbSet<CardDb>> _mockCardsDbSet = new();
    private readonly CardRepository _cardRepository;

    public CardRepositoryTests()
    {
        var cardsData = new List<CardDb>().AsQueryable();

        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.Provider).Returns(cardsData.Provider);
        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.Expression).Returns(cardsData.Expression);
        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.ElementType).Returns(cardsData.ElementType);
        using var enumerator = cardsData.GetEnumerator();
        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

        _mockContext.Setup(c => c.Cards).Returns(_mockCardsDbSet.Object);

        _cardRepository = new CardRepository(_mockContext.Object);
    }
    [Theory, AutoData]
    public void GetAll_WhenReaderReturnsData_ReturnsListOfCards(
        CardName card1Name, CardSuit card1Suit, CardRank card1Rank,
        CardName card2Name, CardSuit card2Suit, CardRank card2Rank
        )
    {
        // Arrange
        var card1 = new CardDb((int)card1Name, (int)card1Suit, (int)card1Rank);
        var card2 = new CardDb((int)card2Name, (int)card2Suit, (int)card2Rank);

        var cardsData = new List<CardDb> { card1, card2 }.AsQueryable();

        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.Provider).Returns(cardsData.Provider);
        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.Expression).Returns(cardsData.Expression);
        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.ElementType).Returns(cardsData.ElementType);
        using var enumerator = cardsData.GetEnumerator();
        _mockCardsDbSet.As<IQueryable<CardDb>>().Setup(m =>
            m.GetEnumerator()).Returns(enumerator);

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

        _mockContext.Verify(c => c.Cards, Times.Once);
    }

    [Fact]
    public void GetAll_WhenReaderReturnsNoData_ReturnsEmptyList()
    {
        // Act
        var result = _cardRepository.GetAll;

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        _mockContext.Verify(c => c.Cards, Times.Once);
    }
}
