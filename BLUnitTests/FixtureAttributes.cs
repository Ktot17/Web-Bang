using AutoFixture;
using AutoFixture.Xunit2;
using BLComponent;

namespace BLUnitTests;

[AttributeUsage(AttributeTargets.Method)]
public class PlayerAutoDataAttribute(
    bool createWeapon = false,
    int beerAmount = 0,
    int health = 5,
    int maxHealth = 5
)
    : AutoDataAttribute(() =>
    {
        var fixture = new Fixture();
        fixture.Customize<Card>(c =>
            c.FromFactory((CardSuit suit, CardRank rank) => CardFactory.CreateCard(CardName.Beer, suit, rank)));
        fixture.Customize<WeaponCard>(c =>
            c.FromFactory((CardSuit suit, CardRank rank) =>
                (WeaponCard)CardFactory.CreateCard(CardName.Schofield, suit, rank)));
        fixture.Customize<Player>(c =>
            c.FromFactory(() => new Player(fixture.Create<Guid>(), fixture.Create<string>(),
                fixture.Create<PlayerRole>(), health, maxHealth,
                new PLayerCards(createWeapon ? fixture.Create<WeaponCard>() : null,
                    fixture.CreateMany<Card>(beerAmount).ToList(),
                    fixture.CreateMany<Card>(beerAmount).ToList()))));
        return fixture;
    });

public class DeckAutoDataCustomization(int drawPileCount = 0,
    int discardPileCount = 0, CardSuit suit = CardSuit.Clubs) : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<Card>(c =>
            c.FromFactory((CardRank rank) => CardFactory.CreateCard(CardName.Beer, suit, rank)));
        fixture.Customize<Deck>(c => c.FromFactory(() =>
            new Deck(fixture.CreateMany<Card>(drawPileCount).ToList(),
                fixture.CreateMany<Card>(discardPileCount).ToList())));
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class DeckAutoDataAttribute(int drawPileCount = 0, int discardPileCount = 0, CardSuit suit = CardSuit.Clubs)
    : AutoDataAttribute(() =>
    {
        var fixture = new Fixture().Customize(new DeckAutoDataCustomization(drawPileCount, discardPileCount, suit));
        return fixture;
    });

[AttributeUsage(AttributeTargets.Method)]
public class CardAutoDataAttribute(CardName name) : AutoDataAttribute(() =>
{
    var fixture = new Fixture();
    fixture.Customize<Card>(composer =>
        composer.FromFactory(() =>
            CardFactory.CreateCard(name,
                fixture.Create<CardSuit>(), fixture.Create<CardRank>())));
    return fixture;
});

[AttributeUsage(AttributeTargets.Method)]
public class GameManagerAutoDataAttribute(int playerNamesAmount = 0,
    int drawPileCount = 0, int discardPileCount = 0, CardSuit suit = CardSuit.Clubs)
    : AutoDataAttribute(() =>
    {
        var fixture = new Fixture();
        fixture.Customize(new CompositeCustomization(new DeckAutoDataCustomization(drawPileCount, discardPileCount, suit)));
        var ids = fixture.CreateMany<Guid>(playerNamesAmount).ToList();
        var names = fixture.CreateMany<string>(playerNamesAmount).ToList();
        fixture.Customize<Dictionary<Guid, string>>(composer =>
            composer.FromFactory(() =>
                ids.Zip(names).ToDictionary(pair => pair.First, pair => pair.Second)));
        return fixture;
    });
