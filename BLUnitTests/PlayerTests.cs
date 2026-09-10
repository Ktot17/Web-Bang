using BLComponent;
using BLComponent.InputPorts;
using Moq;

namespace BLUnitTests;

public class PlayerTests
{
    private readonly Mock<ICardRepository> _cardRepositoryMock = new();

    public PlayerTests()
    {
        _cardRepositoryMock.Setup(repo => repo.GetAll).Returns([]);
    }

    private static List<Player> GetPlayers(int beerAmount, int playerAmount)
    {
        var cards = Enumerable.Repeat(
                CardFactory.CreateCard(CardName.Beer, CardSuit.Clubs, CardRank.Ace), beerAmount)
            .ToList();
        return new PlayerCollectionBuilder().
            AddPlayer(p => p.WithCardsInHand(cards)).
            AddPlayers(playerAmount - 1).Players;
    }

    [Theory, PlayerAutoData]
    public void AddCardInHand_Test(Player player, Bang card)
    {
        // Act
        player.AddCardInHand(card);

        // Assert
        Assert.Single(player.CardsInHand);
        Assert.Equal(card.Name, player.CardsInHand[0].Name);
        Assert.Equal(card.Suit, player.CardsInHand[0].Suit);
        Assert.Equal(card.Rank, player.CardsInHand[0].Rank);
    }

    [Theory, PlayerAutoData]
    public void AddCardOnBoard_Test(Player player, Barrel card)
    {
        // Act
        player.AddCardOnBoard(card);

        // Assert
        Assert.Single(player.CardsOnBoard);
        Assert.Equal(card.Name, player.CardsOnBoard[0].Name);
        Assert.Equal(card.Suit, player.CardsOnBoard[0].Suit);
        Assert.Equal(card.Rank, player.CardsOnBoard[0].Rank);
    }

    [Theory, PlayerAutoData]
    public void ChangeWeapon_EquipTest(Player player, Volcanic card)
    {
        // Act
        var weapon = player.ChangeWeapon(card);

        // Assert
        Assert.Null(weapon);
        Assert.NotNull(player.Weapon);
        Assert.Equal(card.Name, player.Weapon.Name);
        Assert.Equal(card.Suit, player.Weapon.Suit);
        Assert.Equal(card.Rank, player.Weapon.Rank);
    }

    [Theory, PlayerAutoData(createWeapon: true)]
    public void ChangeWeapon_ChangeTest(Player player, Winchester newWeapon)
    {
        // Arrange
        var startWeapon = player.Weapon;

        // Act
        var oldWeapon = player.ChangeWeapon(newWeapon);

        // Assert
        Assert.Equivalent(startWeapon, oldWeapon);
        Assert.NotNull(player.Weapon);
        Assert.Equal(newWeapon.Name, player.Weapon.Name);
        Assert.Equal(newWeapon.Suit, player.Weapon.Suit);
        Assert.Equal(newWeapon.Rank, player.Weapon.Rank);
    }

    [Theory, PlayerAutoData]
    public void RemoveCard_BadIdTest(Player player)
    {
        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() => player.RemoveCard(Guid.NewGuid()));
    }

    [Theory, PlayerAutoData(beerAmount: 5, createWeapon: true)]
    public void RemoveCard_FromHandTest(Player player)
    {
        // Arrange
        var startCard = player.CardsInHand[1];

        // Act
        var card = player.RemoveCard(startCard.Id);

        // Assert
        Assert.Equivalent(card, startCard);
        Assert.Equal(4, player.CardsInHand.Count);
        Assert.Equal(5, player.CardsOnBoard.Count);
        Assert.NotNull(player.Weapon);
    }

    [Theory, PlayerAutoData(beerAmount: 5, createWeapon: true)]
    public void RemoveCard_FromBoardTest(Player player)
    {
        // Arrange
        var startCard = player.CardsOnBoard[1];

        // Act
        var card = player.RemoveCard(startCard.Id);

        // Assert
        Assert.Equivalent(card, startCard);
        Assert.Equal(5, player.CardsInHand.Count);
        Assert.Equal(4, player.CardsOnBoard.Count);
        Assert.NotNull(player.Weapon);
    }

    [Theory, PlayerAutoData(beerAmount: 5, createWeapon: true)]
    public void RemoveCard_WeaponTest(Player player)
    {
        // Arrange
        var startCard = player.Weapon!;

        // Act
        var card = player.RemoveCard(startCard.Id);

        // Assert
        Assert.Equivalent(card, startCard);
        Assert.Equal(5, player.CardsInHand.Count);
        Assert.Equal(5, player.CardsOnBoard.Count);
        Assert.Null(player.Weapon);
    }

    [Theory, PlayerAutoData]
    public void ApplyDamage_UsualTest(Player player)
    {
        // Act
        var rc = player.ApplyDamage(2, null!);

        // Assert
        Assert.True(rc);
        Assert.False(player.IsDead);
        Assert.False(player.IsDeadOnThisTurn);
        Assert.Equal(player.MaxHealth - 2, player.Health);
    }

    [Theory, PlayerAutoData]
    public void ApplyDamage_DeathTest(Player player)
    {
        // Act
        var rc = player.ApplyDamage(player.MaxHealth, null!);

        // Assert
        Assert.False(rc);
        Assert.True(player.IsDead);
        Assert.True(player.IsDeadOnThisTurn);
        Assert.Equal(0, player.Health);
    }

    [Fact]
    public void ApplyDamage_OneBeerDeathTest()
    {
        // Arrange
        var players = GetPlayers(1, 4);

        // Act
        var rc = players[0].ApplyDamage(players[0].MaxHealth, new GameState(players,
            new Deck(_cardRepositoryMock.Object), players[0].Id, new WaitCondition()));

        // Assert
        Assert.True(rc);
        Assert.False(players[0].IsDead);
        Assert.False(players[0].IsDeadOnThisTurn);
        Assert.Equal(1, players[0].Health);
    }

    [Fact]
    public void ApplyDamage_ManyBeerDeathTest()
    {
        // Arrange
        var players = GetPlayers(3, 4);

        // Act
        var rc = players[0].ApplyDamage(players[0].MaxHealth + 2, new GameState(players,
            new Deck(_cardRepositoryMock.Object), players[0].Id, new WaitCondition()));

        // Assert
        Assert.True(rc);
        Assert.False(players[0].IsDead);
        Assert.False(players[0].IsDeadOnThisTurn);
        Assert.Equal(1, players[0].Health);
    }

    [Fact]
    public void ApplyDamage_TwoPlayersBeerDeathTest()
    {
        // Arrange
        var players = GetPlayers(1, 2);

        // Act
        var rc = players[0].ApplyDamage(players[0].MaxHealth, new GameState(players,
            new Deck(_cardRepositoryMock.Object), players[0].Id, new WaitCondition()));

        // Assert
        Assert.False(rc);
        Assert.True(players[0].IsDead);
        Assert.True(players[0].IsDeadOnThisTurn);
        Assert.Equal(0, players[0].Health);
    }

    [Fact]
    public void ApplyDamage_ThreePlayersBeerDeathTest()
    {
        // Arrange
        var players = GetPlayers(1, 3);

        // Act
        var rc = players[0].ApplyDamage(players[0].MaxHealth, new GameState(players,
            new Deck(_cardRepositoryMock.Object),
            players[0].Id, new WaitCondition()));

        // Assert
        Assert.True(rc);
        Assert.False(players[0].IsDead);
        Assert.False(players[0].IsDeadOnThisTurn);
        Assert.Equal(1, players[0].Health);
    }

    [Theory, PlayerAutoData]
    public void Heal_MaxHealthTest(Player player)
    {
        // Act
        var rc = player.Heal(100);

        // Assert
        Assert.True(rc);
        Assert.False(player.IsDead);
        Assert.Equal(player.MaxHealth, player.Health);
    }

    [Theory, PlayerAutoData(health: 3)]
    public void Heal_UsualTest(Player player)
    {
        // Act
        var rc = player.Heal(1);

        // Assert
        Assert.False(rc);
        Assert.False(player.IsDead);
        Assert.Equal(player.MaxHealth - 1, player.Health);
    }

    [Theory, PlayerAutoData(health: -1)]
    public void Heal_ReviveTest(Player player)
    {
        // Act
        var rc = player.Heal(2);

        // Assert
        Assert.False(rc);
        Assert.False(player.IsDead);
        Assert.Equal(1, player.Health);
    }
}
