using BLComponent;
using BLComponent.InputPorts;
using Moq;
using Serilog;
using Xunit.Abstractions;
using Xunit.Sdk;
using static System.DateTime;

namespace BLUnitTests;

public class SimpleRandomOrderer : ITestCaseOrderer
{
    private readonly Random _random = new(Now.Millisecond);

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(
        IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        return testCases.OrderBy(_ => _random.Next());
    }
}

[TestCaseOrderer(ordererTypeName: "BLUnitTests.SimpleRandomOrderer", ordererAssemblyName: "BLUnitTests")]
public class GameManagerTests
{
    private readonly Mock<ICardRepository> _cardRepositoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly GameManagerBuilder _gameManagerBuilder;

    public GameManagerTests()
    {
        _cardRepositoryMock.Setup(repo => repo.GetAll).Returns([]);
        _gameManagerBuilder = new GameManagerBuilder(_cardRepositoryMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public void GameInit_WrongNumberOfPlayers_ThrowsException(int playerCount)
    {
        // Arrange
        var players = Enumerable.Range(0, playerCount)
            .ToDictionary(_ => Guid.NewGuid(), i => $"Player{i + 1}");

        var gameManager = _gameManagerBuilder.Build();

        // Act & Assert
        Assert.Throws<WrongNumberOfPlayersException>(() => gameManager.GameInit(players));
    }

    [Theory, GameManagerAutoData(playerNamesAmount: 6)]
    public void GameInit_NotUniqueNamesTest(Dictionary<Guid, string> players)
    {
        // Arrange
        var gameManager = _gameManagerBuilder.Build();
        var ids = players.Keys.ToList();
        players[ids[0]] = players[ids[1]];
        players[ids[2]] = players[ids[3]];

        // Act & Assert
        Assert.Throws<NotUniqueNamesException>(() => gameManager.GameInit(players));
    }

    [Theory, GameManagerAutoData(playerNamesAmount: 4)]
    public void GameInit_4RolesTests(Dictionary<Guid, string> players)
    {
        // Arrange
        var gameManager = _gameManagerBuilder.Build();
        var expectedRoles = new List<PlayerRole> { PlayerRole.Outlaw,
            PlayerRole.Outlaw, PlayerRole.Renegade, PlayerRole.Sheriff };

        // Act
        gameManager.GameInit(players);

        // Assert
        Assert.Equal(expectedRoles, gameManager.Players.Select(p => p.Role).Order());
    }

    [Theory, GameManagerAutoData(playerNamesAmount: 5)]
    public void GameInit_5RolesTests(Dictionary<Guid, string> players)
    {
        // Arrange
        var gameManager = _gameManagerBuilder.Build();
        var expectedRoles = new List<PlayerRole> { PlayerRole.DeputySheriff, PlayerRole.Outlaw,
            PlayerRole.Outlaw, PlayerRole.Renegade, PlayerRole.Sheriff };

        // Act
        gameManager.GameInit(players);

        // Assert
        Assert.Equal(expectedRoles, gameManager.Players.Select(p => p.Role).Order());
    }

    [Theory, GameManagerAutoData(playerNamesAmount: 6)]
    public void GameInit_6RolesTests(Dictionary<Guid, string> players)
    {
        // Arrange
        var gameManager = _gameManagerBuilder.Build();
        var expectedRoles = new List<PlayerRole> { PlayerRole.DeputySheriff, PlayerRole.Outlaw,
            PlayerRole.Outlaw, PlayerRole.Outlaw, PlayerRole.Renegade, PlayerRole.Sheriff };

        // Act
        gameManager.GameInit(players);

        // Assert
        Assert.Equal(expectedRoles, gameManager.Players.Select(p => p.Role).Order());
    }

    [Theory, GameManagerAutoData(playerNamesAmount: 7)]
    public void GameInit_7RolesTests(Dictionary<Guid, string> players)
    {
        // Arrange
        var gameManager = _gameManagerBuilder.Build();
        var expectedRoles = new List<PlayerRole> { PlayerRole.DeputySheriff, PlayerRole.DeputySheriff, PlayerRole.Outlaw,
            PlayerRole.Outlaw, PlayerRole.Outlaw, PlayerRole.Renegade, PlayerRole.Sheriff };

        // Act
        gameManager.GameInit(players);

        // Assert
        Assert.Equal(expectedRoles, gameManager.Players.Select(p => p.Role).Order());
    }

    [Theory, GameManagerAutoData(drawPileCount: 80)]
    public void GameStart_CardsInHandsCountTest(Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff)).AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).
            WithDeck(deck).Build();

        // Act
        gameManager.GameStart();

        // Assert
        foreach (var player in gameManager.Players)
            Assert.Equal(player.Role == PlayerRole.Sheriff ? player.Health + 2 : player.Health, player.CardsInHand.Count);
    }

    [Fact]
    public void PlayCard_NotInHandTest()
    {
        // Arrange
        var players = new PlayerCollectionBuilder().AddPlayers(7).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            gameManager.PlayCard(Guid.NewGuid(), null, null));
    }

    [Theory, GameManagerAutoData]
    public void PlayCard_TooFarTest(Bang bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var id = gameManager.CurPlayer.Id;
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;

        // Act
        var rc = gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id, players[2].Id, null);

        // Assert
        Assert.Equal(CardRc.TooFar, rc);
        Assert.Equal(cardCount, gameManager.CurPlayer.CardsInHand.Count);
        Assert.Equal(2, gameManager.GetRange(id, gameManager.LivePlayers[2].Id));
    }

    [Theory, GameManagerAutoData]
    public void PlayCard_CantPlayTest(Bang bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])
                .WithBangPlayed()).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;

        // Act
        var rc = gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id,
            gameManager.LivePlayers[1].Id, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
        Assert.Equal(cardCount, gameManager.CurPlayer.CardsInHand.Count);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void PlayCard_OkTest(Card bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;
        gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id, gameManager.LivePlayers[1].Id, null);

        // Act
        var rc = gameManager.CompleteWait(gameManager.LivePlayers[1].Id, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(cardCount - 1, gameManager.CurPlayer.CardsInHand.Count);
    }

    [Theory, GameManagerAutoData(drawPileCount: 2)]
    public void PlayCard_OutlawDeathTest(Deck deck)
    {
        // Arrange
        var bang = CardFactory.CreateCard(CardName.Bang, CardSuit.Clubs, CardRank.Ace);
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])).
            AddPlayer(p => p.WithHealth(1)).
            AddPlayers(5).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).
            WithDeck(deck).Build();
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;
        var id = gameManager.CurPlayer.Id;
        var outlawId = players[1].Id;
        gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id, outlawId, null);

        // Act
        var rc = gameManager.CompleteWait(outlawId, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count - 1, gameManager.LivePlayers.Count);
        Assert.Equal(cardCount + 2, gameManager.CurPlayer.CardsInHand.Count);
        Assert.Equal(id, gameManager.CurPlayer.Id);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void PlayCard_RenegadeDeathTest(Card bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])).
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Renegade).WithHealth(1)).
            AddPlayers(5).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;
        var id = gameManager.CurPlayer.Id;
        var renegadeId = players[1].Id;
        gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id, renegadeId, null);

        // Act
        var rc = gameManager.CompleteWait(renegadeId, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count - 1, gameManager.LivePlayers.Count);
        Assert.Equal(cardCount - 1, gameManager.CurPlayer.CardsInHand.Count);
        Assert.Equal(id, gameManager.CurPlayer.Id);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void PlayCard_DeputyDeathTest(Card bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])).
            AddPlayer(p => p.WithPlayerRole(PlayerRole.DeputySheriff).WithHealth(1)).
            AddPlayers(5).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var id = gameManager.CurPlayer.Id;
        var deputyId = players[1].Id;
        gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id, deputyId, null);

        // Act
        var rc = gameManager.CompleteWait(deputyId, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count - 1, gameManager.LivePlayers.Count);
        Assert.Empty(gameManager.CurPlayer.CardsInHand);
        Assert.Equal(id, gameManager.CurPlayer.Id);
    }

    [Theory, GameManagerAutoData(drawPileCount: 2)]
    public void PlayCard_CurPlayerDeathTest(Deck deck, Bang bang)
    {
        // Arrange
        var duel = CardFactory.CreateCard(CardName.Duel, CardSuit.Clubs, CardRank.Ace);
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])).
            AddPlayer(p => p.WithHealth(1).WithCardsInHand([duel])).
            AddPlayers(5).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[1].Id).
            WithDeck(deck).Build();
        var nextPlayer = players[2];
        var cardCount = nextPlayer.CardsInHand.Count;
        gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id, players[0].Id, null);
        gameManager.CompleteWait(players[0].Id, null, true);

        // Act
        var rc = gameManager.CompleteWait(players[1].Id, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count - 1, gameManager.LivePlayers.Count);
        Assert.Equal(nextPlayer.Id, gameManager.CurPlayer.Id);
        Assert.Equal(cardCount + 2, gameManager.CurPlayer.CardsInHand.Count);
    }

    [Theory, CardAutoData(CardName.Indians)]
    public void PlayCard_IndiansTest(Card indians)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([indians])).
            AddPlayer(p => p.WithPlayerRole(PlayerRole.DeputySheriff).WithHealth(1)).
            AddPlayers(5).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var id = gameManager.CurPlayer.Id;
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;
        gameManager.PlayCard(indians.Id, null, null);
        for (var i = 1; i < players.Count - 1; ++i)
            gameManager.CompleteWait(players[i].Id, null, false);

        // Act
        var rc = gameManager.CompleteWait(players[^1].Id, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count - 1, gameManager.LivePlayers.Count);
        Assert.Equal(cardCount - 1, gameManager.CurPlayer.CardsInHand.Count);
        Assert.Equal(id, gameManager.CurPlayer.Id);
    }

    [Theory, CardAutoData(CardName.Duel)]
    public void PlayCard_DuelTest(Bang bang, Card duel)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang, duel])).
            AddPlayer(p => p.WithPlayerRole(PlayerRole.DeputySheriff).WithCardsInHand([bang])).
            AddPlayers(5).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var id = gameManager.CurPlayer.Id;
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;
        gameManager.PlayCard(duel.Id, players[1].Id, null);
        gameManager.CompleteWait(players[1].Id, null, true);
        gameManager.CompleteWait(players[0].Id, null, true);

        // Act
        var rc = gameManager.CompleteWait(players[1].Id, null, true);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count, gameManager.LivePlayers.Count);
        Assert.Equal(cardCount - 2, gameManager.CurPlayer.CardsInHand.Count);
        Assert.Equal(id, gameManager.CurPlayer.Id);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void PlayCard_LastPlayerKillsPlayerTest(Card bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithHealth(1)).
            AddPlayers(5).
            AddPlayer(p => p.WithCardsInHand([bang])).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[^1].Id).Build();
        var id = gameManager.CurPlayer.Id;
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;
        gameManager.PlayCard(gameManager.CurPlayer.CardsInHand[0].Id, players[0].Id, null);

        // Act
        var rc = gameManager.CompleteWait(players[0].Id, null, true);

        // Assert
        Assert.Equal(CardRc.OutlawWin, rc);
        Assert.Equal(players.Count - 1, gameManager.LivePlayers.Count);
        Assert.Equal(cardCount - 1, gameManager.CurPlayer.CardsInHand.Count);
        Assert.Equal(id, gameManager.CurPlayer.Id);
    }

    [Fact]
    public void DiscardCard_NotInHandTest()
    {
        // Arrange
        var players = new PlayerCollectionBuilder().AddPlayers(7).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() => gameManager.DiscardCard(Guid.NewGuid()));
    }

    [Theory, GameManagerAutoData]
    public void DiscardCard_Test(Bang bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsInHand([bang])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var cardCount = gameManager.CurPlayer.CardsInHand.Count;

        // Act
        gameManager.DiscardCard(bang.Id);

        // Assert
        Assert.Equal(cardCount - 1, gameManager.CurPlayer.CardsInHand.Count);
        Assert.NotNull(gameManager.TopDiscardedCard);
        Assert.Equal(bang.Id, gameManager.TopDiscardedCard!.Id);
    }

    [Theory, GameManagerAutoData]
    public void EndTurn_TooMuchCardsTest(Bang bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithHealth(1).
                WithCardsInHand([bang, bang])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();
        var playerId = gameManager.CurPlayer.Id;

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.CantEndTurn, rc);
        Assert.Equal(playerId, gameManager.CurPlayer.Id);
    }

    [Theory, GameManagerAutoData(drawPileCount: 2)]
    public void EndTurn_NoCardsOnBoardTest(Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff)).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).
            WithDeck(deck).Build();
        var playerId = gameManager.LivePlayers[1].Id;
        var playerCardCount = gameManager.LivePlayers[1].CardsInHand.Count;

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(playerId, gameManager.CurPlayer.Id);
        Assert.Equal(playerCardCount + 2, gameManager.CurPlayer.CardsInHand.Count);
    }

    [Theory, GameManagerAutoData(drawPileCount: 1, suit: CardSuit.Spades)]
    public void EndTurn_DynamiteSheriffDeathTest(Deck deck, Dynamite dynamite)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithHealth(2).
                WithCardsOnBoard([dynamite])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).
            WithCurrentPlayerId(players[^1].Id).WithDeck(deck).Build();

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.OutlawWin, rc);
    }

    [Theory, GameManagerAutoData(drawPileCount: 3, suit: CardSuit.Spades)]
    public void EndTurn_DynamiteDeathTest(Deck deck, Dynamite dynamite)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff)).
            AddPlayer(p => p.WithHealth(2).WithCardsOnBoard([dynamite])).
            AddPlayers(5).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).
            WithCurrentPlayerId(players[0].Id).WithDeck(deck).Build();
        var playerId = players[1].Id;

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count - 1, gameManager.LivePlayers.Count);
        Assert.NotEqual(playerId, gameManager.CurPlayer.Id);
    }

    [Theory, GameManagerAutoData(drawPileCount: 3, suit: CardSuit.Spades)]
    public void EndTurn_DynamiteWithoutDeathTest(Deck deck, Dynamite dynamite)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsOnBoard([dynamite])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).
            WithCurrentPlayerId(players[^1].Id).WithDeck(deck).Build();
        var playerId = players[0].Id;

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count, gameManager.LivePlayers.Count);
        Assert.Equal(playerId, gameManager.CurPlayer.Id);
    }

    [Theory, GameManagerAutoData]
    public void EndTurn_DynamiteReviveTest(Dynamite dynamite, BeerBarrel beerBarrel)
    {
        // Arrange
        var players =
            new PlayerCollectionBuilder().
                AddPlayer(p => p.WithHealth(2).
                    WithCardsOnBoard([dynamite, beerBarrel]).
                    WithPlayerRole(PlayerRole.Sheriff)).
                AddPlayers(3).Players;
        var gameManager = _gameManagerBuilder.
            WithPlayers(players).WithCurrentPlayerId(players[3].Id).
            WithDeck(new Deck([CardFactory.CreateCard(CardName.Bang, CardSuit.Clubs, CardRank.Seven),
                CardFactory.CreateCard(CardName.Bang, CardSuit.Spades, CardRank.Seven)], [])).Build();
        var playerId = players[0].Id;

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players.Count, gameManager.LivePlayers.Count);
        Assert.Equal(playerId, gameManager.CurPlayer.Id);
        Assert.Equal(1, gameManager.CurPlayer.Health);
    }

    [Theory, GameManagerAutoData(drawPileCount: 1, suit: CardSuit.Clubs)]
    public void EndTurn_BeerBarrelTest(Deck deck, BeerBarrel beerBarrel)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithHealth(2).
                WithCardsOnBoard([beerBarrel])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).
            WithCurrentPlayerId(players[^1].Id).WithDeck(deck).Build();

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(4, gameManager.CurPlayer.Health);
    }

    [Theory, GameManagerAutoData(drawPileCount: 1)]
    public void EndTurn_JailTest(Deck deck, Jail jail)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithCardsOnBoard([jail])).
            AddPlayers(6).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[^1].Id).
            WithDeck(deck).Build();
        var playerId = players[0].Id;

        // Act
        var rc = gameManager.EndTurn();

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.NotEqual(playerId, gameManager.CurPlayer.Id);
    }

    [Fact]
    public void CheckEndGame_SheriffWinTest()
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff)).
            AddPlayer(p => p.WithHealth(0)).
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Renegade).WithHealth(0)).
            AddPlayer(p => p.WithHealth(0)).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();

        // Act
        var rc = gameManager.CheckEndGame();

        // Assert
        Assert.Equal(CardRc.SheriffWin, rc);
        Assert.Single(gameManager.LivePlayers);
    }

    [Fact]
    public void CheckEndGame_RenegadeWinTest()
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff).WithHealth(0)).
            AddPlayer(p => p.WithHealth(0)).
            AddPlayer(p => p.WithPlayerRole(PlayerRole.Renegade)).
            AddPlayer(p => p.WithHealth(0)).Players;
        var gameManager = _gameManagerBuilder.WithPlayers(players).WithCurrentPlayerId(players[0].Id).Build();

        // Act
        var rc = gameManager.CheckEndGame();

        // Assert
        Assert.Equal(CardRc.RenegadeWin, rc);
        Assert.Single(gameManager.LivePlayers);
    }
}
