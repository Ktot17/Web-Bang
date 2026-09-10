using BLComponent.InputPorts;
using BLComponent.OutputPort;
using Serilog;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BLUnitTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DBUnitTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BLIntegrationTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("E2ETests")]
namespace BLComponent;

public class GameManager : IGameManager
{
    private const int MinPlayersCountConst = 4;
    private const int MaxPlayersCountConst = 7;
    private const int OutlawRewardCardCount = 3;

    private GameState _gameState = null!;
    private readonly Random _random = new();

    private readonly List<PlayerRole> _roles =
        [PlayerRole.Renegade,
            PlayerRole.Outlaw,
            PlayerRole.Outlaw,
            PlayerRole.DeputySheriff,
            PlayerRole.Outlaw,
            PlayerRole.DeputySheriff];

    private readonly ICardRepository _cardRepository;
    private readonly ILogger _logger;

    public static int MinPlayersCount => MinPlayersCountConst;
    public static int MaxPlayersCount => MaxPlayersCountConst;
    public IReadOnlyList<Player> Players => _gameState.Players;
    public Player CurPlayer => _gameState.CurrentPlayer;
    public Card? TopDiscardedCard => _gameState.CardDeck.TopDiscardedCard;
    public IReadOnlyList<Player> LivePlayers => _gameState.LivePlayers;
    public IReadOnlyList<Player> DeadPlayers => _gameState.DeadPlayers;
    public IReadOnlyList<Card> CardsInDeck => _gameState.CardDeck.DrawPile;

    public IList<Card> GetAllCards
    {
        get
        {
            var cards = new List<Card>();
            cards.AddRange(_gameState.CardDeck.DrawPile);
            cards.AddRange(_gameState.CardDeck.DiscardPile);
            foreach (var player in Players)
            {
                cards.AddRange(player.CardsInHand);
                cards.AddRange(player.CardsOnBoard);
                if (player.Weapon is not null)
                    cards.Add(player.Weapon);
            }

            return cards;
        }
    }

    public GameStateDto GetGameState => new(_gameState);

    public GameManager(ICardRepository cardRepository, ILogger logger)
    {
        _cardRepository = cardRepository;
        _logger = logger;
    }

    internal GameManager(GameState gameState, ICardRepository cardRepository, ILogger logger)
    {
        _cardRepository = cardRepository;
        _logger = logger;
        _gameState = gameState;
    }

    public void GameInit(IDictionary<Guid, string> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        _logger.Information("Инициализация игры.");
        if (users.Count is < MinPlayersCountConst or > MaxPlayersCountConst)
        {
            var ex = new WrongNumberOfPlayersException(users.Count);
            _logger.Error(ex, "Неправильное количество игроков.");
            throw ex;
        }

        if (users.Values.Distinct().Count() != users.Values.Count)
        {
            var ex = new NotUniqueNamesException();
            _logger.Error(ex, "Есть игроки с одинаковыми именами.");
            throw ex;
        }

        _gameState = CreateGameState(users);
    }

    public void GameStart()
    {
        foreach (var player in _gameState.Players)
            for (var i = 0; i < player.MaxHealth; ++i)
                player.AddCardInHand(_gameState.CardDeck.Draw());
        CurPlayer.AddCardInHand(_gameState.CardDeck.Draw());
        CurPlayer.AddCardInHand(_gameState.CardDeck.Draw());
    }

    public CardRc CompleteWait(Guid playerId, Guid? cardId, bool? yesOrNo)
    {
        if (_gameState.WaitCondition.PlayerId is null)
            return CardRc.NoWait;
        if (_gameState.WaitCondition.PlayerId != playerId)
            return CardRc.WrongPlayer;
        var waitingCard = _gameState.WaitCondition.WaitingCard;
        var rc = waitingCard!.WaitComplete(_gameState, cardId, yesOrNo);

        return rc is CardRc.Ok ? AfterPlay(waitingCard) : rc;
    }

    public CardRc PlayCard(Guid cardId, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (_gameState.WaitCondition.PlayerId is not null)
            return CardRc.WaitConditions;

        var curCard = CurPlayer.CardsInHand.FirstOrDefault(x => x.Id == cardId);
        if (curCard is null)
        {
            var ex = new NotExistingGuidException();
            _logger.Error(
                ex,
                "Игрок {PlayerName} разыграл несуществующую карту.",
                CurPlayer.Name);
            throw ex;
        }
        _logger.Information(
            "Игрок {PlayerName} разыграл карту {CardName}.",
            CurPlayer.Name,
            curCard.Name);
        CardRc rc;
        try
        {
            rc = curCard.Play(_gameState, targetPlayerId, targetCardId);
        }
        catch (NotExistingGuidException ex)
        {
            _logger.Error(
                ex,
                "Игрок {PlayerName} выбрал несуществующего игрока/карту.",
                CurPlayer.Name);
            throw new NotExistingGuidException();
        }

        if (rc is CardRc.Ok)
            return AfterPlay(curCard);
        _logger.Information(
            "Игрок {PlayerName} не смог разыграть карту {CardName}.({Rc})",
            CurPlayer.Name,
            curCard.Name,
            rc);
        return rc;
    }

    public void DiscardCard(Guid cardId)
    {
        if (_gameState.WaitCondition.PlayerId is not null)
            return;
        try
        {
            _gameState.CardDeck.Discard(CurPlayer.RemoveCard(cardId));
        }
        catch (NotExistingGuidException ex)
        {
            _logger.Error(
                ex,
                "Игрок {PlayerName} сбросил несуществующую карту.",
                CurPlayer.Name);
            throw new NotExistingGuidException();
        }
        _logger.Information(
            "Игрок {PlayerName} сбросил карту.",
            CurPlayer.Name);
    }

    public CardRc EndTurn()
    {
        if (!CanProceedDueToWaitConditions())
            return CardRc.WaitConditions;

        var isFirstIteration = true;

        while (true)
        {
            if (isFirstIteration && !CanPlayerEndTurn())
                return CardRc.CantEndTurn;

            isFirstIteration = false;
            ProcessPlayerEndTurn();
            ApplyStartOfTurnEffects();

            var rc = CheckEndGame();
            if (rc != CardRc.Ok)
                return rc;

            if (CurPlayer.IsDead)
                continue;

            if (PlayerInJail())
                continue;

            DrawCardsForPlayer();
            return CardRc.Ok;
        }
    }

    public int GetRange(Guid playerId, Guid targetId) =>
        _gameState.Range(playerId, targetId);

    public void SetGameState(GameStateDto gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        _gameState = new GameState(gameState);
    }

    internal CardRc CheckEndGame()
    {
        if (Players[0].IsDead)
        {
            var rc =
                Players.Any(x => x.Role != PlayerRole.Renegade && !x.IsDead)
                    ? CardRc.OutlawWin
                    : CardRc.RenegadeWin;
            _logger.Information(
                "Игра закончилась победой {WhoWon}",
                rc is CardRc.OutlawWin ? "бандитов." : "ренегата.");
            return rc;
        }

        if (!Players.Any(x => x is { Role: PlayerRole.Outlaw, IsDead: false })
            && !Players.Any(x => x is { Role: PlayerRole.Renegade, IsDead: false }))
        {
            _logger.Information("Игра закончилась победой шерифа.");
            return CardRc.SheriffWin;
        }

        foreach (var player in DeadPlayers.Where(x => x.IsDeadOnThisTurn))
        {
            PlayerClear(player.Id);
            player.DeadEarlier();
        }

        if (CurPlayer.IsDead)
            EndTurn();

        return CardRc.Ok;
    }

    private void ShuffleGuids(List<Guid> guids)
    {
        var n = guids.Count;
        while (n > 1)
        {
            --n;
            var k = _random.Next(n + 1);
            (guids[k], guids[n]) = (guids[n], guids[k]);
        }
    }

    private GameState CreateGameState(IDictionary<Guid, string> users)
    {
        var ids = users.Keys.ToList();
        ShuffleGuids(ids);
        var players = new List<Player> { new(ids[0], users[ids[0]], PlayerRole.Sheriff, 5) };
        var roles = _roles.ToList();
        _logger.Information(
            "Игрок {UserName}. Роль {Sheriff}.",
            users[ids[0]],
            PlayerRole.Sheriff);
        for (var i = 1; i < ids.Count; ++i)
        {
            var k = _random.Next(ids.Count - i);
            players.Add(new Player(ids[i], users[ids[i]], roles[k], 4));
            _logger.Information(
                "Игрок {UserName}. Роль {Role}.",
                users[ids[i]],
                roles[k]);
            roles.RemoveAt(k);
        }

        return new GameState(players, new Deck(_cardRepository), players[0].Id);
    }

    private void PlayerClear(Guid playerId)
    {
        var player = Players.First(x => x.Id == playerId);
        var cardsInHandCount = player.CardsInHand.Count;
        var cardsOnBoardCount = player.CardsOnBoard.Count;
        for (var i = 0; i < cardsInHandCount; ++i)
            _gameState.CardDeck.Discard(player.RemoveCard(player.CardsInHand[0].Id));
        for (var i = 0; i < cardsOnBoardCount; ++i)
            _gameState.CardDeck.Discard(player.RemoveCard(player.CardsOnBoard[0].Id));
        if (player.Weapon is not null)
            _gameState.CardDeck.Discard(player.RemoveCard(player.Weapon.Id));
    }

    private void CheckBounties()
    {
        foreach (var playerRole in DeadPlayers
            .Where(x => x.IsDeadOnThisTurn)
            .Select(x => x.Role))
            switch (playerRole)
            {
                case PlayerRole.Outlaw:
                    for (var i = 0; i < OutlawRewardCardCount; ++i)
                        CurPlayer.AddCardInHand(_gameState.CardDeck.Draw());
                    break;
                case PlayerRole.DeputySheriff when CurPlayer.Role is PlayerRole.Sheriff:
                    PlayerClear(CurPlayer.Id);
                    break;
                case PlayerRole.Renegade:
                case PlayerRole.Sheriff:
                case PlayerRole.DeputySheriff:
                    break;
                default:
                    var ex = new NotExistingRoleException();
                    _logger.Fatal(ex, "Несуществующая роль.");
                    throw ex;
            }
    }

    private CardRc AfterPlay(Card card)
    {
        if (card.Type is CardType.Instant)
            DiscardCard(card.Id);
        else
            CurPlayer.RemoveCard(card.Id);
        if (card.Name is not CardName.Indians)
            CheckBounties();
        return CheckEndGame();
    }

    private bool CanProceedDueToWaitConditions()
    {
        return _gameState.WaitCondition.PlayerId is null;
    }

    private bool CanPlayerEndTurn()
    {
        if (CurPlayer.CardsInHand.Count <= CurPlayer.Health)
            return true;

        _logger.Information(
            "Игрок {PlayerName} не смог закончить ход.",
            CurPlayer.Name);
        return false;
    }

    private void ProcessPlayerEndTurn()
    {
        _logger.Information(
            "Игрок {PlayerName} закончил ход.",
            CurPlayer.Name);
        CurPlayer.EndTurn();
        _gameState.NextPlayer();
    }

    private void ApplyStartOfTurnEffects()
    {
        ApplyCardEffect(CardName.Dynamite, "взорвался динамит", true);
        ApplyCardEffect(CardName.BeerBarrel, "открылась бочка с пивом", false);
    }

    private void ApplyCardEffect(CardName cardName, string message, bool isDynamite)
    {
        var card = CurPlayer.CardsOnBoard.FirstOrDefault(x => x.Name == cardName);
        if (card is null)
            return;

        _logger.Information(
            "У игрока {PlayerName} {Message}.",
            CurPlayer.Name,
            message);
        if (isDynamite)
            ((Dynamite)card).ApplyEffect(_gameState);
        else
            ((BeerBarrel)card).ApplyEffect(_gameState);
    }

    private bool PlayerInJail()
    {
        var card = CurPlayer.CardsOnBoard.FirstOrDefault(x => x.Name == CardName.Jail);
        if (card is not Jail jail || !jail.ApplyEffect(_gameState))
            return false;
        _logger.Information(
            "Игрок {PlayerName} пропускает ход в тюрьме.",
            CurPlayer.Name);
        return true;
    }

    private void DrawCardsForPlayer()
    {
        CurPlayer.AddCardInHand(_gameState.CardDeck.Draw());
        CurPlayer.AddCardInHand(_gameState.CardDeck.Draw());
    }
}
