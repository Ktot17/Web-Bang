using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace BLComponent;

[Table("classicdeck")]
public class CardDb
{
    [Column("name")]
    public int Name { get; init; }

    [Column("suit")]
    public int Suit { get; init; }

    [Column("rank")]
    public int Rank { get; init; }

    public CardDb() { }

    public CardDb(int name, int suit, int rank)
    {
        Name = name;
        Suit = suit;
        Rank = rank;
    }
}

public record Options
{
    [JsonProperty]
    public Guid TargetId { get; set; } = Guid.Empty;

    [JsonProperty]
    public bool? YesOrNo { get; set; }

    [JsonProperty]
    public IReadOnlyList<Guid> StoreCardsIds { get; set; } = [];

    [JsonProperty]
    public IReadOnlyList<bool> Answers { get; set; } = [];

    [JsonProperty]
    public int Shift { get; set; }

    public Options() { }

    internal Options(Guid targetId, bool? yesOrNo, List<Guid> storeCardsIds, List<bool> answers, int shift)
    {
        TargetId = targetId;
        YesOrNo = yesOrNo;
        StoreCardsIds = storeCardsIds;
        Answers = answers;
        Shift = shift;
    }
}

public record CardDto
{
    [JsonProperty]
    public Guid Id { get; private set; }

    [JsonProperty]
    public CardSuit Suit { get; private set; }

    [JsonProperty]
    public CardRank Rank { get; private set; }

    [JsonProperty]
    public CardName Name { get; private set; }

    [JsonProperty]
    public CardType Type { get; private set; }

    [JsonProperty]
    public Options? Options { get; private set; }

    public CardDto() { }

    internal CardDto(Card card)
    {
        Id = card.Id;
        Suit = card.Suit;
        Rank = card.Rank;
        Name = card.Name;
        Type = card.Type;
        Options = card.Options;
    }
}

public abstract class Card(Guid id, CardSuit suit, CardRank rank, Options? options)
{
    internal abstract CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId);

    internal abstract CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo);

    public Guid Id { get; } = id;
    public CardSuit Suit { get; } = suit;
    public CardRank Rank { get; } = rank;
    public CardName Name { get; protected init; }
    public CardType Type { get; protected init; }
    public Options? Options { get; protected init; } = options;

    internal static void Shoot(GameState state, Guid playerId, bool yesOrNo)
    {
        var target = state.Players.First(x => x.Id == playerId);
        var barrel = target.CardsOnBoard.FirstOrDefault(x => x.Name == CardName.Barrel);
        if (barrel is not null && ((Barrel)barrel).ApplyEffect(state, playerId))
            return;

        var missed = target.CardsInHand.FirstOrDefault(x => x.Name == CardName.Missed);
        if (yesOrNo && missed is not null)
            state.CardDeck.Discard(target.RemoveCard(missed.Id));
        else
            target.ApplyDamage(1, new GameState(state.Players, state.CardDeck, target.Id));
    }
}
