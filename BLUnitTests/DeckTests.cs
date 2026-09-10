using BLComponent.InputPorts;
using Moq;
using BLComponent;

namespace BLUnitTests;

public class DeckTests
{
    private readonly Mock<ICardRepository> _cardRepositoryMock = new();

    [Fact]
    public void Constructor_WithCardRepository_InitializesDrawPile()
    {
        // Arrange
        _cardRepositoryMock.Setup(repo => repo.GetAll).Returns([]);

        // Act
        var deck = new Deck(_cardRepositoryMock.Object);

        // Assert
        Assert.NotNull(deck.DrawPile);
        Assert.NotNull(deck.DiscardPile);
        Assert.Empty(deck.DiscardPile);
    }

    [Theory, DeckAutoData(drawPileCount: 1)]
    public void Draw_WithCardsInDrawPile_ReturnsTopCard(Deck deck)
    {
        // Arrange
        var card = deck.DrawPile[^1];

        // Act
        var result = deck.Draw();

        // Assert
        Assert.Equal(card, result);
        Assert.Empty(deck.DrawPile);
    }

    [Theory, DeckAutoData(discardPileCount: 1)]
    public void Draw_WithEmptyDrawPileButDiscardPile_ShufflesAndReturnsCard(Deck deck)
    {
        // Arrange
        var card = deck.DiscardPile[0];

        // Act
        var result = deck.Draw();

        // Assert
        Assert.Equal(card, result);
        Assert.Empty(deck.DiscardPile);
        Assert.Empty(deck.DrawPile);
    }

    [Theory, DeckAutoData]
    public void Discard_Card_AddsToDiscardPile(Deck deck, Bang card)
    {
        // Act
        deck.Discard(card);

        // Assert
        Assert.Single(deck.DiscardPile);
        Assert.Equal(card, deck.DiscardPile[0]);
    }

    [Theory, DeckAutoData]
    public void TopDiscardedCard_WithEmptyDiscardPile_ReturnsNull(Deck deck)
    {
        // Act
        var result = deck.TopDiscardedCard;

        // Assert
        Assert.Null(result);
    }

    [Theory, DeckAutoData(discardPileCount: 2)]
    public void TopDiscardedCard_WithCardsInDiscardPile_ReturnsLastCard(Deck deck)
    {
        // Arrange
        var card = deck.DiscardPile[^1];

        // Act
        var result = deck.TopDiscardedCard;

        // Assert
        Assert.Equal(card, result);
    }
}
