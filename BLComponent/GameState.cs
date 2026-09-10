using Newtonsoft.Json;

namespace BLComponent;

public record GameStateDto
{
    [JsonProperty]
    public IReadOnlyList<PlayerDto> Players { get; set; } = [];

    [JsonProperty]
    public DeckDto CardDeck { get; set; } = new();

    [JsonProperty]
    public Guid CurrentPlayerId { get; set; }

    [JsonProperty]
    public WaitConditionDto WaitCondition { get; set; } = new();

    public GameStateDto() { }

    internal GameStateDto(GameState state)
    {
        Players = state.Players.Select(x => new PlayerDto(x)).ToList();
        CardDeck = new DeckDto(state.CardDeck);
        CurrentPlayerId = state.CurrentPlayerId;
        WaitCondition = new WaitConditionDto(state.WaitCondition);
    }
}

public record WaitConditionDto
{
    [JsonProperty]
    public Guid? PlayerId { get; set; }

    [JsonProperty]
    public CardDto? WaitingCard { get; set; }

    [JsonProperty]
    public IReadOnlyList<CardDto>? Cards { get; set; }

    [JsonProperty]
    public Guid? TargetPlayerId { get; set; }

    [JsonProperty]
    public IReadOnlyList<Guid> GeneralStoreCards { get; set; } = [];

    [JsonProperty]
    public IReadOnlyList<bool> Answers { get; set; } = [];

    public WaitConditionDto() { }

    internal WaitConditionDto(WaitCondition waitCondition)
    {
        PlayerId = waitCondition.PlayerId;
        WaitingCard = waitCondition.WaitingCard is null
            ? null
            : new CardDto(waitCondition.WaitingCard);
        if (waitCondition.Cards is not null)
            Cards = waitCondition.Cards.Select(x => new CardDto(x)).ToList();
        TargetPlayerId = waitCondition.TargetPlayerId;
        GeneralStoreCards = waitCondition.GeneralStoreCards;
        Answers = waitCondition.Answers;
    }
}

public class WaitCondition
{
    internal Guid? PlayerId { get; set; }
    internal Card? WaitingCard { get; set; }
    internal List<Card>? Cards { get; set; }
    internal Guid? TargetPlayerId { get; set; }
    internal List<Guid> GeneralStoreCards { get; set; } = [];
    internal List<bool> Answers { get; set; } = [];

    internal WaitCondition() { }

    internal WaitCondition(WaitConditionDto waitCondition)
    {
        PlayerId = waitCondition.PlayerId;
        if (waitCondition.WaitingCard is not null)
            WaitingCard = CardFactory.CreateCard(
                waitCondition.WaitingCard.Id,
                waitCondition.WaitingCard.Name,
                waitCondition.WaitingCard.Suit,
                waitCondition.WaitingCard.Rank,
                waitCondition.WaitingCard.Options);
        if (waitCondition.Cards is not null)
            Cards = waitCondition.Cards.Select(x =>
                CardFactory.CreateCard(x.Id, x.Name, x.Suit, x.Rank, x.Options)).ToList();
        TargetPlayerId = waitCondition.TargetPlayerId;
        GeneralStoreCards = waitCondition.GeneralStoreCards.ToList();
        Answers = waitCondition.Answers.ToList();
    }
}

public class GameState
{
    private int _currentPlayerIndex;

    internal List<Player> Players { get; }
    internal Deck CardDeck { get; }
    internal Guid CurrentPlayerId { get; private set; }
    internal Player CurrentPlayer => Players.First(x => x.Id == CurrentPlayerId);
    internal WaitCondition WaitCondition { get; } = new();
    internal IReadOnlyList<Player> LivePlayers => [.. Players.Where(x => !x.IsDead)];
    internal IReadOnlyList<Player> DeadPlayers => [.. Players.Where(x => x.IsDead)];

    internal GameState(IReadOnlyList<Player> players, Deck deck, Guid currentPlayerId)
    {
        _currentPlayerIndex = players.ToList().FindIndex(x => x.Id == currentPlayerId);
        Players = [.. players];
        CardDeck = deck;
        CurrentPlayerId = currentPlayerId;
    }

    internal GameState(
        IReadOnlyList<Player> players,
        Deck deck,
        Guid currentPlayerId,
        WaitCondition waitCondition) : this(players, deck, currentPlayerId)
    {
        WaitCondition = waitCondition;
    }

    internal GameState(GameStateDto dto)
    {
        Players = [];
        foreach (var playerDto in dto.Players)
            Players.Add(new Player(playerDto));
        CardDeck = new Deck(dto.CardDeck);
        CurrentPlayerId = dto.CurrentPlayerId;
        _currentPlayerIndex = Players.FindIndex(x => x.Id == CurrentPlayerId);
        WaitCondition = new WaitCondition(dto.WaitCondition);
    }

    internal int Range(Guid playerId, Guid targetId)
    {
        var playerIndex = LivePlayers.ToList().FindIndex(x => x.Id == playerId);
        var targetIndex = LivePlayers.ToList().FindIndex(x => x.Id == targetId);
        var add = LivePlayers[targetIndex].CardsOnBoard.Any(x => x.Name == CardName.Mustang)
            ? 1
            : 0;
        var sub = LivePlayers[playerIndex].CardsOnBoard.Any(x => x.Name == CardName.Scope)
            ? 1
            : 0;

        return int.Min(
            LivePlayers.Count - int.Abs(playerIndex - targetIndex),
            int.Abs(playerIndex - targetIndex)) + add - sub;
    }

    internal Player NextPlayer(int shift)
    {
        var index = _currentPlayerIndex;
        do
        {
            index = (index + shift) % Players.Count;
        } while (Players[index].IsDead);
        return Players[index];
    }

    internal void NextPlayer()
    {
        do
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Count;
        } while (Players[_currentPlayerIndex].IsDead);
        CurrentPlayerId = Players[_currentPlayerIndex].Id;
    }
}
