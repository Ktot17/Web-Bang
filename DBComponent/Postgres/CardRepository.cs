using BLComponent;
using BLComponent.InputPorts;
using Microsoft.EntityFrameworkCore;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DBUnitTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BLIntegrationTests")]
namespace DBComponent.Postgres;

public class CardRepository(ServerDbContext context) : ICardRepository
{
    public IEnumerable<Card> GetAll => context.Cards.AsNoTracking().Select(card =>
        CardFactory.CreateCard((CardName)card.Name, (CardSuit)card.Suit, (CardRank)card.Rank));
}
