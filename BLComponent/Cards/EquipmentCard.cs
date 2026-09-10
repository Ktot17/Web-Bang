namespace BLComponent;

public abstract class EquipmentCard : Card
{
    protected EquipmentCard(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Type = CardType.Equipment;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;
}

public abstract class NotJail(Guid id, CardSuit suit, CardRank rank) : EquipmentCard(id, suit, rank)
{
    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (state.CurrentPlayer.CardsOnBoard.Any(x => x.Name == Name))
            return CardRc.CantPlay;
        state.CurrentPlayer.AddCardOnBoard(this);
        return CardRc.Ok;
    }
}

public sealed class Barrel : NotJail
{
    public Barrel(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Barrel;
    }

    internal bool ApplyEffect(GameState state, Guid playerId)
    {
        var card = state.CardDeck.Draw();
        state.CardDeck.Discard(card);
        var player = state.Players.First(x => x.Id == playerId);
        if (card.Suit is not CardSuit.Hearts)
            return false;
        state.CardDeck.Discard(player.RemoveCard(Id));
        return true;
    }
}

public sealed class Scope : NotJail
{
    public Scope(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Scope;
    }
}

public sealed class Mustang : NotJail
{
    public Mustang(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Mustang;
    }
}

public sealed class Dynamite : NotJail
{
    private const int DynamiteDamage = 3;

    public Dynamite(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Dynamite;
    }

    internal void ApplyEffect(GameState state)
    {
        var card = state.CardDeck.Draw();
        var player = state.CurrentPlayer;
        player.RemoveCard(Id);
        if (card.Suit is not CardSuit.Spades || card.Rank is < CardRank.Two or > CardRank.Nine)
        {
            var next = state.NextPlayer(1);
            next.AddCardOnBoard(this);
        }
        else
        {
            player.ApplyDamage(DynamiteDamage, state);
            state.CardDeck.Discard(this);
        }
    }
}

public sealed class BeerBarrel : NotJail
{
    private const int BeerBarrelHeal = 2;

    public BeerBarrel(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.BeerBarrel;
    }

    internal void ApplyEffect(GameState state)
    {
        var card = state.CardDeck.Draw();
        state.CardDeck.Discard(card);
        var player = state.CurrentPlayer;
        player.RemoveCard(Id);
        if (card.Suit is not CardSuit.Clubs || card.Rank is < CardRank.Two or > CardRank.Nine)
        {
            var next = state.NextPlayer(1);
            next.AddCardOnBoard(this);
        }
        else
        {
            player.Heal(BeerBarrelHeal);
            state.CardDeck.Discard(this);
        }
    }
}

public sealed class Jail : EquipmentCard
{
    public Jail(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank)
    {
        Name = CardName.Jail;
    }

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (targetPlayerId is null)
            return CardRc.NeedTargetPlayer;
        var playerId = targetPlayerId.Value;
        var target = state.Players.Find(x => x.Id == playerId);
        if (target is null)
            throw new NotExistingGuidException();
        if (target.CardsOnBoard.Any(x => x.Name == Name)
            || target.Role is PlayerRole.Sheriff)
            return CardRc.CantPlay;
        target.AddCardOnBoard(this);
        return CardRc.Ok;
    }

    internal bool ApplyEffect(GameState state)
    {
        var card = state.CardDeck.Draw();
        state.CardDeck.Discard(card);
        var player = state.CurrentPlayer;
        player.RemoveCard(Id);
        state.CardDeck.Discard(this);
        return card.Suit is not CardSuit.Hearts;
    }
}
