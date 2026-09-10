using BLComponent;
using BLComponent.InputPorts;
using MongoDB.Driver;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DBUnitTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BLIntegrationTests")]
namespace DBComponent.MongoDb;

public class CardRepository(ServerDbContext context) : ICardRepository
{
    private readonly IMongoCollection<CardDb> _cards = context.Cards;

    public IEnumerable<Card> GetAll =>
        _cards.Find(FilterDefinition<CardDb>.Empty)
            .ToEnumerable()
            .Select(card => CardFactory.CreateCard(
                (CardName)card.Name,
                (CardSuit)card.Suit,
                (CardRank)card.Rank));
}

