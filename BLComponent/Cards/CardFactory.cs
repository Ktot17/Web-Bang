namespace BLComponent;

public static class CardFactory
{
    private static readonly Dictionary<CardName, Func<Guid, CardSuit, CardRank, Options?, Card>> FactoryDictionary = new()
    {
        [CardName.Bang] = (id, s, r, opts) => new Bang(id, s, r, opts),
        [CardName.Beer] = (id, s, r, _) => new Beer(id, s, r),
        [CardName.Missed] = (id, s, r, _) => new Missed(id, s, r),
        [CardName.Panic] = (id, s, r, _) => new Panic(id, s, r),
        [CardName.GeneralStore] = (id, s, r, opts) => new GeneralStore(id, s, r, opts),
        [CardName.Indians] = (id, s, r, opts) => new Indians(id, s, r, opts),
        [CardName.Duel] = (id, s, r, opts) => new Duel(id, s, r, opts),
        [CardName.Gatling] = (id, s, r, opts) => new Gatling(id, s, r, opts),
        [CardName.CatBalou] = (id, s, r, _) => new CatBalou(id, s, r),
        [CardName.Saloon] = (id, s, r, _) => new Saloon(id, s, r),
        [CardName.Stagecoach] = (id, s, r, _) => new Stagecoach(id, s, r),
        [CardName.WellsFargo] = (id, s, r, _) => new WellsFargo(id, s, r),
        [CardName.Barrel] = (id, s, r, _) => new Barrel(id, s, r),
        [CardName.Scope] = (id, s, r, _) => new Scope(id, s, r),
        [CardName.Mustang] = (id, s, r, _) => new Mustang(id, s, r),
        [CardName.Dynamite] = (id, s, r, _) => new Dynamite(id, s, r),
        [CardName.BeerBarrel] = (id, s, r, _) => new BeerBarrel(id, s, r),
        [CardName.Jail] = (id, s, r, _) => new Jail(id, s, r),
        [CardName.Volcanic] = (id, s, r, _) => new Volcanic(id, s, r),
        [CardName.Schofield] = (id, s, r, _) => new Schofield(id, s, r),
        [CardName.Remington] = (id, s, r, _) => new Remington(id, s, r),
        [CardName.Carabine] = (id, s, r, _) => new Carabine(id, s, r),
        [CardName.Winchester] = (id, s, r, _) => new Winchester(id, s, r)
    };

    public static Card CreateCard(CardName name, CardSuit suit, CardRank rank) =>
        CreateCard(Guid.NewGuid(), name, suit, rank, null);

    public static Card CreateCard(Guid id, CardName name, CardSuit suit, CardRank rank, Options? options) =>
        FactoryDictionary[name].Invoke(id, suit, rank, options);
}
