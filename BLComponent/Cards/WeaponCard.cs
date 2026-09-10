using Newtonsoft.Json;

namespace BLComponent;

public record WeaponCardDto : CardDto
{
    [JsonProperty]
    public int Range { get; private set; }

    public WeaponCardDto() { }

    internal WeaponCardDto(WeaponCard card) : base(card)
    {
        Range = card.Range;
    }
}

public abstract class WeaponCard : Card
{
    public int Range { get; protected init; }

    protected WeaponCard(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Type = CardType.Weapon;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        var weapon = state.CurrentPlayer.ChangeWeapon(this);
        if (weapon is not null)
            state.CardDeck.Discard(weapon);
        return CardRc.Ok;
    }
}

public sealed class Volcanic : WeaponCard
{
    private const int VolcanicRange = 1;

    public Volcanic(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Volcanic;
        Range = VolcanicRange;
    }
}

public sealed class Schofield : WeaponCard
{
    private const int SchofieldRange = 2;

    public Schofield(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Schofield;
        Range = SchofieldRange;
    }
}

public sealed class Remington : WeaponCard
{
    private const int RemingtonRange = 3;

    public Remington(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Remington;
        Range = RemingtonRange;
    }
}

public sealed class Carabine : WeaponCard
{
    private const int CarabineRange = 4;

    public Carabine(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Carabine;
        Range = CarabineRange;
    }
}

public sealed class Winchester : WeaponCard
{
    private const int WinchesterRange = 5;

    public Winchester(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Winchester;
        Range = WinchesterRange;
    }
}
