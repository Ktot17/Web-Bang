namespace BLComponent;

public abstract class InstantCard : Card
{
    protected InstantCard(Guid id, CardSuit suit, CardRank rank, Options? options) :
        base(id, suit, rank, options)
    {
        Type = CardType.Instant;
    }
}

public abstract class CardWithOptions : InstantCard
{
    protected CardWithOptions(Guid id, CardSuit suit, CardRank rank, Options? options) :
        base(id, suit, rank, options)
    {
        Options = options ?? new Options();
    }
}

public sealed class Bang : CardWithOptions
{
    public Bang(Guid id, CardSuit suit, CardRank rank, Options? options) : base(id, suit, rank, options)
    {
        Name = CardName.Bang;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo)
    {
        if (yesOrNo is null)
            return CardRc.WrongParameter;
        Options!.YesOrNo = yesOrNo;
        state.WaitCondition.PlayerId = null;
        state.WaitCondition.WaitingCard = null;
        state.WaitCondition.TargetPlayerId = Guid.Empty;
        return Play(state, Options.TargetId, null);
    }

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (targetPlayerId is null)
            return CardRc.NeedTargetPlayer;
        Options!.TargetId = targetPlayerId.Value;
        var player = state.CurrentPlayer;
        if (player.IsBangPlayed && (player.Weapon is null || player.Weapon.Name != CardName.Volcanic))
            return CardRc.CantPlay;
        var playerId = targetPlayerId.Value;
        if (state.Players.Find(x => x.Id == playerId) is null)
            throw new NotExistingGuidException();
        if (player.Range < state.Range(state.CurrentPlayerId, playerId))
            return CardRc.TooFar;
        if (Options.YesOrNo is null)
        {
            state.WaitCondition.PlayerId = targetPlayerId;
            state.WaitCondition.WaitingCard = this;
            state.WaitCondition.TargetPlayerId = Options.TargetId;
            return CardRc.WaitConditions;
        }
        player.BangPlayed();
        Shoot(state, playerId, Options.YesOrNo.Value);
        Options.YesOrNo = null;
        return CardRc.Ok;
    }
}

public sealed class Beer : InstantCard
{
    private const int NoBeerPlayerCount = 2;

    public Beer(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Name = CardName.Beer;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        var player = state.CurrentPlayer;
        if (state.LivePlayers.Count + (state.CurrentPlayer.IsDead ? 1 : 0) <= NoBeerPlayerCount
            || player.Heal(1))
            return CardRc.CantPlay;
        return CardRc.Ok;
    }
}

public sealed class Missed : InstantCard
{
    public Missed(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Name = CardName.Missed;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        return CardRc.CantPlay;
    }
}

public sealed class Panic : InstantCard
{
    public Panic(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Name = CardName.Panic;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (targetPlayerId is null)
            return CardRc.NeedTargetPlayer;
        if (targetCardId is null)
            return CardRc.NeedTargetCard;
        var playerId = targetPlayerId.Value;
        var target = state.Players.Find(x => x.Id == playerId);
        if (target is null)
            throw new NotExistingGuidException();
        if (1 < state.Range(state.CurrentPlayerId, playerId))
            return CardRc.TooFar;
        if (target.CardCount == 0)
            return CardRc.CantPlay;
        var cardId = targetCardId.Value;
        var card = target.RemoveCard(cardId);
        state.CurrentPlayer.AddCardInHand(card);
        return CardRc.Ok;
    }
}

public sealed class GeneralStore : CardWithOptions
{
    public GeneralStore(Guid id, CardSuit suit, CardRank rank, Options? options) :
        base(id, suit, rank, options)
    {
        Name = CardName.GeneralStore;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo)
    {
        if (cardId is null || !state.WaitCondition.Cards!.Exists(x => x.Id == cardId.Value))
            return CardRc.WrongParameter;
        var tmp = Options!.StoreCardsIds.ToList();
        tmp.Add(cardId.Value);
        Options!.StoreCardsIds = tmp;
        var nextPlayer = state.NextPlayer(Options.Shift++);
        if (Options.StoreCardsIds.Count == state.LivePlayers.Count)
        {
            state.WaitCondition.PlayerId = null;
            state.WaitCondition.WaitingCard = null;
            state.WaitCondition.Cards = null;
            state.WaitCondition.GeneralStoreCards = [];
            return Play(state, null, null);
        }
        else
        {
            state.WaitCondition.PlayerId = nextPlayer.Id;
            state.WaitCondition.Cards =
                state.WaitCondition.Cards!.Where(x => x.Id != cardId).ToList();
            state.WaitCondition.GeneralStoreCards.Add(cardId.Value);
        }
        return CardRc.Ok;
    }

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (Options!.StoreCardsIds.Count != state.LivePlayers.Count)
        {
            state.WaitCondition.PlayerId = state.CurrentPlayerId;
            state.WaitCondition.WaitingCard = this;
            state.WaitCondition.Cards = state.CardDeck.TopCards(state.LivePlayers.Count);
            state.WaitCondition.GeneralStoreCards = [];
            Options.Shift = 1;
            return CardRc.WaitConditions;
        }
        var cards = new List<Card>();
        for (var i = 0; i < state.LivePlayers.Count; ++i)
            cards.Add(state.CardDeck.Draw());
        for (var i = 0; i < state.LivePlayers.Count; ++i)
        {
            var card = cards.Find(x => x.Id == Options.StoreCardsIds[i]);
            state.CurrentPlayer.AddCardInHand(card!);
            state.NextPlayer();
        }

        Options.StoreCardsIds = [];
        Options.Shift = 1;
        return CardRc.Ok;
    }
}

public sealed class Indians : CardWithOptions
{
    public Indians(Guid id, CardSuit suit, CardRank rank, Options? options) :
        base(id, suit, rank, options)
    {
        Name = CardName.Indians;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo)
    {
        if (yesOrNo is null)
            return CardRc.WrongParameter;
        var tmp = Options!.Answers.ToList();
        tmp.Add(yesOrNo.Value);
        Options.Answers = tmp;
        var nextPlayer = state.NextPlayer(Options.Shift++);
        if (Options.Answers.Count == state.LivePlayers.Count - 1)
        {
            state.WaitCondition.PlayerId = null;
            state.WaitCondition.WaitingCard = null;
            state.WaitCondition.Answers = [];
            return Play(state, null, null);
        }

        state.WaitCondition.PlayerId = nextPlayer.Id;
        state.WaitCondition.Answers.Add(yesOrNo.Value);
        return CardRc.Ok;
    }

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (Options!.Answers.Count != state.LivePlayers.Count - 1)
        {
            Options.Shift = 1;
            var nextPlayer = state.NextPlayer(Options.Shift++);
            state.WaitCondition.PlayerId = nextPlayer.Id;
            state.WaitCondition.WaitingCard = this;
            state.WaitCondition.Answers = [];
            return CardRc.WaitConditions;
        }
        var i = 0;
        foreach (var player in state.LivePlayers.Where(x => x.Id != state.CurrentPlayerId))
        {
            var card = player.CardsInHand.FirstOrDefault(x => x.Name is CardName.Bang);
            if (Options.Answers[i++] && card is not null)
            {
                player.RemoveCard(card.Id);
                state.CardDeck.Discard(card);
            }
            else
            {
                player.ApplyDamage(
                    1,
                    new GameState(state.Players, state.CardDeck, player.Id));
            }
        }

        Options.Answers = [];
        Options.Shift = 1;
        return CardRc.Ok;
    }
}

public sealed class Duel : CardWithOptions
{
    private const int PlayersInAction = 2;

    public Duel(Guid id, CardSuit suit, CardRank rank, Options? options) :
        base(id, suit, rank, options)
    {
        Name = CardName.Duel;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo)
    {
        if (yesOrNo is null)
            return CardRc.WrongParameter;

        if (yesOrNo.Value)
            yesOrNo = state.Players.Find(x => x.Id == state.WaitCondition.PlayerId)!
                        .CardsInHand
                        .Count(x => x.Name is CardName.Bang)
                >= Options!.Answers.Count / PlayersInAction + 1;

        var tmp = Options!.Answers.ToList();
        tmp.Add(yesOrNo.Value);
        Options.Answers = tmp;
        if (yesOrNo.Value)
        {
            state.WaitCondition.PlayerId = state.WaitCondition.PlayerId == Options.TargetId
                ? state.CurrentPlayerId
                : Options.TargetId;
            state.WaitCondition.Answers.Add(yesOrNo.Value);
        }
        else
        {
            state.WaitCondition.PlayerId = null;
            state.WaitCondition.WaitingCard = null;
            state.WaitCondition.TargetPlayerId = Guid.Empty;
            state.WaitCondition.Answers = [];
            return Play(state, Options.TargetId, null);
        }

        return CardRc.Ok;
    }

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (targetPlayerId is null)
            return CardRc.NeedTargetPlayer;
        Options!.TargetId = targetPlayerId.Value;
        var playerId = targetPlayerId.Value;
        var target = state.Players.Find(x => x.Id == playerId);
        if (target is null)
            throw new NotExistingGuidException();
        if (Options.Answers.Count == 0)
        {
            state.WaitCondition.PlayerId = targetPlayerId;
            state.WaitCondition.WaitingCard = this;
            state.WaitCondition.TargetPlayerId = Options.TargetId;
            state.WaitCondition.Answers = [];
            return CardRc.WaitConditions;
        }
        var curPlayer = target;
        for (var i = 0; i < Options.Answers.Count - 1; ++i)
        {
            var card = curPlayer.CardsInHand.FirstOrDefault(x => x.Name == CardName.Bang)!;
            curPlayer.RemoveCard(card.Id);
            state.CardDeck.Discard(card);
            curPlayer = curPlayer.Id == state.CurrentPlayerId ? target : state.CurrentPlayer;
        }

        curPlayer.ApplyDamage(
            1,
            new GameState(state.Players, state.CardDeck, curPlayer.Id));
        Options.Answers = [];
        return CardRc.Ok;
    }
}

public sealed class Gatling : CardWithOptions
{
    public Gatling(Guid id, CardSuit suit, CardRank rank, Options? options) :
        base(id, suit, rank, options)
    {
        Name = CardName.Gatling;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo)
    {
        if (yesOrNo is null)
            return CardRc.WrongParameter;
        var tmp = Options!.Answers.ToList();
        tmp.Add(yesOrNo.Value);
        Options.Answers = tmp;
        var nextPlayer = state.NextPlayer(Options.Shift++);
        if (Options.Answers.Count == state.LivePlayers.Count - 1)
        {
            state.WaitCondition.PlayerId = null;
            state.WaitCondition.WaitingCard = null;
            state.WaitCondition.Answers = [];
            return Play(state, null, null);
        }

        state.WaitCondition.PlayerId = nextPlayer.Id;
        state.WaitCondition.Answers.Add(yesOrNo.Value);
        return CardRc.Ok;
    }

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (Options!.Answers.Count != state.LivePlayers.Count - 1)
        {
            Options.Shift = 1;
            var nextPlayer = state.NextPlayer(Options.Shift++);
            state.WaitCondition.PlayerId = nextPlayer.Id;
            state.WaitCondition.WaitingCard = this;
            state.WaitCondition.Answers = [];
            return CardRc.WaitConditions;
        }
        var i = 0;
        foreach (var player in state.LivePlayers.Where(x => x.Id != state.CurrentPlayerId))
            Shoot(state, player.Id, Options.Answers[i++]);

        Options.Answers = [];
        Options.Shift = 1;
        return CardRc.Ok;
    }
}

public sealed class CatBalou : InstantCard
{
    public CatBalou(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Name = CardName.CatBalou;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        if (targetPlayerId is null)
            return CardRc.NeedTargetPlayer;
        if (targetCardId is null)
            return CardRc.NeedTargetCard;
        var playerId = targetPlayerId.Value;
        var target = state.Players.Find(x => x.Id == playerId);
        if (target is null)
            throw new NotExistingGuidException();
        if (target.CardCount == 0)
            return CardRc.CantPlay;
        var cardId = targetCardId.Value;
        var card = target.RemoveCard(cardId);
        state.CardDeck.Discard(card);
        return CardRc.Ok;
    }
}

public sealed class Saloon : InstantCard
{
    private const int PlayerHealAmount = 1;

    public Saloon(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Name = CardName.Saloon;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        foreach (var player in state.LivePlayers)
            player.Heal(player == state.CurrentPlayer ? PlayerHealAmount + 1 : PlayerHealAmount);

        return CardRc.Ok;
    }
}

public sealed class Stagecoach : InstantCard
{
    private const int CardDrawCount = 2;

    public Stagecoach(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Name = CardName.Stagecoach;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        for (var i = 0; i < CardDrawCount; i++)
            state.CurrentPlayer.AddCardInHand(state.CardDeck.Draw());

        return CardRc.Ok;
    }
}

public sealed class WellsFargo : InstantCard
{
    private const int CardDrawCount = 3;

    public WellsFargo(Guid id, CardSuit suit, CardRank rank) : base(id, suit, rank, null)
    {
        Name = CardName.WellsFargo;
    }

    internal override CardRc WaitComplete(GameState state, Guid? cardId, bool? yesOrNo) =>
        CardRc.Ok;

    internal override CardRc Play(GameState state, Guid? targetPlayerId, Guid? targetCardId)
    {
        for (var i = 0; i < CardDrawCount; i++)
            state.CurrentPlayer.AddCardInHand(state.CardDeck.Draw());

        return CardRc.Ok;
    }
}
