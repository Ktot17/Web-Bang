using AutoFixture.Xunit2;
using BLComponent;

namespace BLUnitTests;

public class CardsTests
{
    private static List<Player> GetPlayerList(int playerAmount)
    {
        return new PlayerCollectionBuilder()
            .AddPlayer(p => p.WithPlayerRole(PlayerRole.Sheriff))
            .AddPlayers(playerAmount - 1).Players;
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void Play_BangOkTest(Card bang)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        bang.Play(gameState, players[1].Id, null);

        // Act
        var rc = bang.WaitComplete(gameState, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void Play_BangWaitTest(Card bang)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = bang.Play(gameState, players[1].Id, null);

        // Assert
        Assert.Equal(CardRc.WaitConditions, rc);
        Assert.NotNull(gameState.WaitCondition.PlayerId);
        Assert.Equal(gameState.WaitCondition.PlayerId, players[1].Id);
        Assert.NotNull(gameState.WaitCondition.WaitingCard);
        Assert.Equal(gameState.WaitCondition.WaitingCard.Id, bang.Id);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void Play_TwoBangsTest(Card bang)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        bang.Play(gameState, players[1].Id, null);
        bang.WaitComplete(gameState, null, false);

        // Act
        var rc = bang.Play(gameState, players[1].Id, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void Play_TwoBangsWithVolcanicTest(Card bang, Volcanic volcanic)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer(p => p.WithWeapon(volcanic))
            .AddPlayers(3).Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        bang.Play(gameState, players[1].Id, null);
        bang.WaitComplete(gameState, null, false);
        bang.Play(gameState, players[1].Id, null);

        // Act
        var rc = bang.WaitComplete(gameState, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void Play_BangTooFarTest(Card bang)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = bang.Play(gameState, players[2].Id, null);

        // Assert
        Assert.Equal(CardRc.TooFar, rc);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void WaitComplete_BangWrongParameterTest(Card bang)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        bang.Play(gameState, players[1].Id, null);

        // Act
        var rc = bang.WaitComplete(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.WrongParameter, rc);
    }

    [Theory, AutoData]
    public void Play_MissedTest(Missed missed)
    {
        // Act
        var rc = missed.Play(null!, null, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
    }

    [Theory, DeckAutoData(drawPileCount: 1, suit: CardSuit.Hearts)]
    public void Play_BarrelOkTest(Barrel barrel, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer()
            .AddPlayer(p => p.WithCardsOnBoard([barrel]))
            .AddPlayers(2).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var bang = CardFactory.CreateCard(CardName.Bang, CardSuit.Clubs, CardRank.Ace);
        bang.Play(gameState, players[1].Id, null);

        // Act
        var rc = bang.WaitComplete(gameState, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[1].MaxHealth, players[1].Health);
        Assert.Empty(players[1].CardsOnBoard);
    }

    [Theory, DeckAutoData(drawPileCount: 1, suit: CardSuit.Spades)]
    public void Play_BarrelNotOkTest(Barrel barrel, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer()
            .AddPlayer(p => p.WithCardsOnBoard([barrel]))
            .AddPlayers(2).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var bang = CardFactory.CreateCard(CardName.Bang, CardSuit.Clubs, CardRank.Ace);
        bang.Play(gameState, players[1].Id, null);

        // Act
        var rc = bang.WaitComplete(gameState, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[1].MaxHealth - 1, players[1].Health);
        Assert.NotEmpty(players[1].CardsOnBoard);
    }

    [Theory, DeckAutoData]
    public void Play_BangMissedTest(Missed missed, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer()
            .AddPlayer(p => p.WithCardsInHand([missed]))
            .AddPlayers(2).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var bang = CardFactory.CreateCard(CardName.Bang, CardSuit.Clubs, CardRank.Ace);
        bang.Play(gameState, players[1].Id, null);

        // Act
        var rc = bang.WaitComplete(gameState, null, true);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[1].MaxHealth, players[1].Health);
        Assert.Empty(players[1].CardsInHand);
    }

    [Theory, AutoData]
    public void Play_BeerMaxHealthTest(Beer beer)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = beer.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
        Assert.Equal(players[0].MaxHealth, players[0].Health);
    }

    [Theory, AutoData]
    public void Play_BeerTwoPlayersTest(Beer beer)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer(p => p.WithHealth(4))
            .AddPlayer().Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = beer.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
        Assert.Equal(players[0].MaxHealth - 1, players[0].Health);
    }

    [Theory, AutoData]
    public void Play_BeerOkTest(Beer beer)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer(p => p.WithHealth(4))
            .AddPlayers(3).Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = beer.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[0].MaxHealth, players[0].Health);
    }

    [Theory, AutoData]
    public void Play_PanicTooFarTest(Panic panic)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = panic.Play(gameState, players[2].Id, Guid.NewGuid());

        // Assert
        Assert.Equal(CardRc.TooFar, rc);
    }

    [Theory, AutoData]
    public void Play_PanicCantPlayTest(Panic panic)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = panic.Play(gameState, players[1].Id, Guid.NewGuid());

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void Play_PanicOkTest(Panic panic, Card bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer()
            .AddPlayer(p => p.WithCardsInHand([bang]))
            .AddPlayers(2)
            .Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = panic.Play(gameState, players[1].Id, players[1].CardsInHand[0].Id);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Empty(players[1].CardsInHand);
        Assert.Single(players[0].CardsInHand);
    }

    [Theory, DeckAutoData(drawPileCount: 4)]
    public void Play_GeneralStoreWrongCardTest(Deck deck)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var generalStore = CardFactory
            .CreateCard(CardName.GeneralStore, CardSuit.Clubs, CardRank.Ace);
        generalStore.Play(gameState, null, null);

        // Act
        var rc = generalStore.WaitComplete(gameState, Guid.Empty, null);

        // Assert
        Assert.Equal(CardRc.WrongParameter, rc);
    }

    [Theory, DeckAutoData(drawPileCount: 4)]
    public void Play_GeneralStoreOkTest(Deck deck)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var cards = deck.DrawPile;
        var generalStore = CardFactory
            .CreateCard(CardName.GeneralStore, CardSuit.Clubs, CardRank.Ace);
        generalStore.Play(gameState, null, null);
        generalStore.WaitComplete(gameState, cards[0].Id, null);
        generalStore.WaitComplete(gameState, cards[1].Id, null);
        generalStore.WaitComplete(gameState, cards[2].Id, null);

        // Act
        var rc = generalStore.WaitComplete(gameState, cards[3].Id, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        foreach (var player in players)
            Assert.Single(player.CardsInHand);
    }

    [Theory, CardAutoData(CardName.Indians)]
    public void Play_IndiansDeathTest(Card indians)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer().
            AddPlayer(p => p.WithHealth(1)).
            AddPlayers(2).Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        indians.Play(gameState, null, null);
        for (var i = 1; i < players.Count - 1; ++i)
            indians.WaitComplete(gameState, null, false);

        // Act
        var rc = indians.WaitComplete(gameState, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[0].MaxHealth, players[0].Health);
        Assert.True(players[1].IsDead);
        Assert.Equal(players[2].MaxHealth - 1, players[2].Health);
        Assert.Equal(players[3].MaxHealth - 1, players[3].Health);
    }

    [Theory, DeckAutoData]
    public void Play_IndiansOkTest(Bang bang, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer().
            AddPlayer(p => p.WithCardsInHand([bang])).
            AddPlayers(2).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var indians = CardFactory.CreateCard(CardName.Indians, CardSuit.Clubs, CardRank.Ace);
        indians.Play(gameState, null, null);
        for (var i = 1; i < players.Count - 1; ++i)
            indians.WaitComplete(gameState, null, true);

        // Act
        var rc = indians.WaitComplete(gameState, null, true);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[0].MaxHealth, players[0].Health);
        Assert.Equal(players[1].MaxHealth, players[1].Health);
        Assert.Empty(players[1].CardsInHand);
        Assert.Equal(players[2].MaxHealth - 1, players[2].Health);
        Assert.Equal(players[3].MaxHealth - 1, players[3].Health);
    }

    [Theory, DeckAutoData]
    public void Play_DuelDeathTest(Bang bang, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithCardsInHand([bang, bang]).WithHealth(1)).
            AddPlayer(p => p.WithCardsInHand([bang, bang, bang])).
            AddPlayers(2).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var duel = CardFactory.CreateCard(CardName.Duel, CardSuit.Clubs, CardRank.Ace);
        duel.Play(gameState, players[1].Id, null);
        for (var i = 0; i < 5; ++i)
            duel.WaitComplete(gameState, null, true);

        // Act
        var rc = duel.WaitComplete(gameState, null, true);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Empty(players[0].CardsInHand);
        Assert.True(players[0].IsDead);
        Assert.Empty(players[1].CardsInHand);
    }

    [Theory, DeckAutoData]
    public void Play_DuelOkTest(Bang bang, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithCardsInHand([bang, bang])).
            AddPlayer(p => p.WithCardsInHand([bang, bang])).
            AddPlayers(2).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        var duel = CardFactory.CreateCard(CardName.Duel, CardSuit.Clubs, CardRank.Ace);
        duel.Play(gameState, players[1].Id, null);
        for (var i = 0; i < 4; ++i)
            duel.WaitComplete(gameState, null, true);

        // Act
        var rc = duel.WaitComplete(gameState, null, true);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[0].MaxHealth, players[0].Health);
        Assert.Empty(players[0].CardsInHand);
        Assert.Equal(players[1].MaxHealth - 1, players[1].Health);
        Assert.Empty(players[1].CardsInHand);
    }

    [Theory, CardAutoData(CardName.Gatling)]
    public void Play_GatlingManyDeathsTest(Card gatling)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer().
            AddPlayer(p => p.WithHealth(1)).
            AddPlayer().
            AddPlayer(p => p.WithHealth(1)).Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        gatling.Play(gameState, null, null);
        for (var i = 1; i < players.Count - 1; ++i)
            gatling.WaitComplete(gameState, null, false);

        // Act
        var rc = gatling.WaitComplete(gameState, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[0].MaxHealth, players[0].Health);
        Assert.True(players[1].IsDead);
        Assert.Equal(players[2].MaxHealth - 1, players[2].Health);
        Assert.True(players[3].IsDead);
    }

    [Theory, CardAutoData(CardName.Gatling)]
    public void Play_GatlingOkTest(Card gatling)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        gatling.Play(gameState, null, null);
        for (var i = 1; i < players.Count - 1; ++i)
            gatling.WaitComplete(gameState, null, false);

        // Act
        var rc = gatling.WaitComplete(gameState, null, false);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(players[0].MaxHealth, players[0].Health);
        Assert.Equal(players[1].MaxHealth - 1, players[1].Health);
        Assert.Equal(players[2].MaxHealth - 1, players[2].Health);
        Assert.Equal(players[3].MaxHealth - 1, players[3].Health);
    }

    [Theory, AutoData]
    public void Play_CatBalouCantPlayTest(CatBalou catBalou)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = catBalou.Play(gameState, players[1].Id, Guid.Empty);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
    }

    [Theory, DeckAutoData]
    public void Play_CatBalouOkTest(CatBalou catBalou, Bang bang, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer().
            AddPlayer().
            AddPlayer(p => p.WithCardsInHand([bang])).
            AddPlayers(2).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        var rc = catBalou.Play(gameState, players[2].Id, players[2].CardsInHand[0].Id);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Empty(players[1].CardsInHand);
        Assert.Empty(players[0].CardsInHand);
    }

    [Theory, AutoData]
    public void Play_SaloonOkTest(Saloon saloon)
    {
        // Arrange
        var players = new PlayerCollectionBuilder()
            .AddPlayer(p => p.WithHealth(2)).
            AddPlayer(p => p.WithHealth(2)).
            AddPlayer(p => p.WithHealth(4)).
            AddPlayer(p => p.WithHealth(1)).Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = saloon.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(4, players[0].Health);
        Assert.Equal(3, players[1].Health);
        Assert.Equal(5, players[2].Health);
        Assert.Equal(2, players[3].Health);
    }

    [Theory, DeckAutoData(drawPileCount: 2)]
    public void Play_StagecoachOkTest(Stagecoach stagecoach, Deck deck)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        var rc = stagecoach.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(2, players[0].CardsInHand.Count);
    }

    [Theory, DeckAutoData(drawPileCount: 3)]
    public void Play_WellsFargoOkTest(WellsFargo wellsFargo, Deck deck)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        var rc = wellsFargo.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(3, players[0].CardsInHand.Count);
    }

    [Theory, AutoData]
    public void Play_ScopeTest(Scope scope)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = scope.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(0, gameState.Range(players[0].Id, players[1].Id));
        Assert.Equal(1, gameState.Range(players[1].Id, players[0].Id));
    }

    [Theory, AutoData]
    public void Play_TwoScopesTest(Scope scope)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        scope.Play(gameState, null, null);

        // Act
        var rc = scope.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
    }

    [Theory, DeckAutoData(drawPileCount: 1, suit: CardSuit.Spades)]
    public void Play_DynamiteBlowTest(Dynamite dynamite, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
                AddPlayer(p => p.WithCardsOnBoard([dynamite])).
                AddPlayers(3).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        dynamite.ApplyEffect(gameState);

        // Assert
        Assert.Equal(players[0].MaxHealth - 3, players[0].Health);
        Assert.Equal(dynamite.Id, deck.TopDiscardedCard!.Id);
    }

    [Theory, DeckAutoData(drawPileCount: 1, suit: CardSuit.Clubs)]
    public void Play_DynamitePassTest(Dynamite dynamite, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithCardsOnBoard([dynamite])).
            AddPlayers(3).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        dynamite.ApplyEffect(gameState);

        // Assert
        Assert.Equal(players[0].MaxHealth, players[0].Health);
        Assert.Equal(dynamite.Id, players[1].CardsOnBoard[0].Id);
    }

    [Theory, DeckAutoData(drawPileCount: 1, suit: CardSuit.Clubs)]
    public void Play_BeerBarrelHealTest(BeerBarrel beerBarrel, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithCardsOnBoard([beerBarrel]).WithHealth(2)).
            AddPlayers(3).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        beerBarrel.ApplyEffect(gameState);

        // Assert
        Assert.Equal(4, players[0].Health);
        Assert.Equal(beerBarrel.Id, deck.TopDiscardedCard!.Id);
    }

    [Theory, DeckAutoData(drawPileCount: 1, suit: CardSuit.Spades)]
    public void Play_BeerBarrelPassTest(BeerBarrel beerBarrel, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithCardsOnBoard([beerBarrel]).WithHealth(2)).
            AddPlayers(3).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        beerBarrel.ApplyEffect(gameState);

        // Assert
        Assert.Equal(2, players[0].Health);
        Assert.Equal(beerBarrel.Id, players[1].CardsOnBoard[0].Id);
    }

    [Theory, AutoData]
    public void Play_JailCantPlayOnSheriffTest(Jail jail)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[1].Id, new WaitCondition());

        // Act
        var rc = jail.Play(gameState, players[0].Id, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
    }

    [Theory, AutoData]
    public void Play_JailCantPlayTwiceTest(Jail jail)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());
        jail.Play(gameState, players[1].Id, null);

        // Act
        var rc = jail.Play(gameState, players[1].Id, null);

        // Assert
        Assert.Equal(CardRc.CantPlay, rc);
    }

    [Theory, DeckAutoData(drawPileCount: 1, suit: CardSuit.Spades)]
    public void Play_JailSkipTurnTest(Jail jail, Deck deck)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer(p => p.WithCardsOnBoard([jail])).
            AddPlayers(3).Players;
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());

        // Act
        var res = jail.ApplyEffect(gameState);

        // Assert
        Assert.True(res);
        Assert.Equal(jail.Id, deck.TopDiscardedCard!.Id);
    }

    [Theory, AutoData]
    public void Play_Volcanic(Volcanic volcanic)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = volcanic.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(1, players[0].Range);
    }

    [Theory, AutoData]
    public void Play_Schofield(Schofield schofield)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = schofield.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(2, players[0].Range);
    }

    [Theory, AutoData]
    public void Play_Remington(Remington remington)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = remington.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(3, players[0].Range);
    }

    [Theory, AutoData]
    public void Play_Carabine(Carabine carabine)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = carabine.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(4, players[0].Range);
    }

    [Theory, AutoData]
    public void Play_Winchester(Winchester winchester)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act
        var rc = winchester.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(5, players[0].Range);
    }

    [Theory, DeckAutoData]
    public void Play_SchofieldAfterWinchester(Schofield schofield, Winchester winchester, Deck deck)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, deck, players[0].Id, new WaitCondition());
        winchester.Play(gameState, null, null);

        // Act
        var rc = schofield.Play(gameState, null, null);

        // Assert
        Assert.Equal(CardRc.Ok, rc);
        Assert.Equal(2, players[0].Range);
        Assert.Equal(winchester.Id, deck.TopDiscardedCard!.Id);
    }

    [Theory, CardAutoData(CardName.Bang)]
    public void Play_BangTargetDontExist(Card bang)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            bang.Play(gameState, Guid.Empty, null));
    }

    [Theory, AutoData]
    public void Play_JailTargetDontExist(Jail jail)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            jail.Play(gameState, Guid.Empty, null));
    }

    [Theory, CardAutoData(CardName.Duel)]
    public void Play_DuelTargetDontExist(Card duel)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            duel.Play(gameState, Guid.Empty, null));
    }

    [Theory, AutoData]
    public void Play_PanicTargetDontExist(Panic panic)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            panic.Play(gameState, Guid.Empty, Guid.Empty));
    }

    [Theory, AutoData]
    public void Play_PanicTargetCardDontExist(Panic panic, Bang bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer().
            AddPlayer(p => p.WithCardsInHand([bang])).
            AddPlayers(2).Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            panic.Play(gameState, players[1].Id, Guid.Empty));
    }

    [Theory, AutoData]
    public void Play_CatBalouTargetDontExist(CatBalou catBalou)
    {
        // Arrange
        var players = GetPlayerList(4);
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            catBalou.Play(gameState, Guid.Empty, Guid.Empty));
    }

    [Theory, AutoData]
    public void Play_CatBalouTargetCardDontExist(CatBalou catBalou, Bang bang)
    {
        // Arrange
        var players = new PlayerCollectionBuilder().
            AddPlayer().
            AddPlayer(p => p.WithCardsInHand([bang])).
            AddPlayers(2).Players;
        var gameState = new GameState(players, null!, players[0].Id, new WaitCondition());

        // Act & Assert
        Assert.Throws<NotExistingGuidException>(() =>
            catBalou.Play(gameState, players[1].Id, Guid.Empty));
    }
}
