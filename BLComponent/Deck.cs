using BLComponent.InputPorts;
using Newtonsoft.Json;

namespace BLComponent;

public record DeckDto
{
    [JsonProperty]
    public IReadOnlyList<CardDto> DrawPile { get; private set; } = [];

    [JsonProperty]
    public IReadOnlyList<CardDto> DiscardPile { get; private set; } = [];

    public DeckDto() { }

    internal DeckDto(Deck deck)
    {
        DrawPile = deck.DrawPile.Select(x => new CardDto(x)).ToList();
        DiscardPile = deck.DiscardPile.Select(x => new CardDto(x)).ToList();
    }
}

public class Deck
{
    private readonly Stack<Card> _drawPile = new();
    private readonly List<Card> _discardPile = [];
    private readonly Random _random = new();

    internal IReadOnlyList<Card> DrawPile => [.. _drawPile];
    internal IReadOnlyList<Card> DiscardPile => _discardPile;

    internal Card? TopDiscardedCard => _discardPile.Count == 0 ? null : _discardPile[^1];

    internal Deck() { }

    internal Deck(List<Card> drawPile, List<Card> discardPile)
    {
        _discardPile = discardPile;
        _drawPile = new Stack<Card>(drawPile);
    }

    internal Deck(ICardRepository cardRepository)
    {
        var cards = cardRepository.GetAll.ToList();
        Shuffle(cards);
        _drawPile = new Stack<Card>(cards);
    }

    internal Deck(DeckDto dto)
    {
        foreach (var cardDto in dto.DrawPile.Reverse())
            _drawPile.Push(CardFactory.CreateCard(
                cardDto.Id,
                cardDto.Name,
                cardDto.Suit,
                cardDto.Rank,
                cardDto.Options));
        foreach (var cardDto in dto.DiscardPile)
            _discardPile.Add(CardFactory.CreateCard(
                cardDto.Id,
                cardDto.Name,
                cardDto.Suit,
                cardDto.Rank,
                cardDto.Options));
    }

    internal List<Card> TopCards(int cardAmount) =>
        _drawPile.Take(cardAmount).ToList();

    internal Card Draw()
    {
        if (_drawPile.Count != 0)
            return _drawPile.Pop();
        Shuffle(_discardPile);
        foreach (var card in _discardPile)
            _drawPile.Push(card);
        _discardPile.Clear();
        return _drawPile.Pop();
    }

    internal void Discard(Card card)
    {
        _discardPile.Add(card);
    }

    private void Shuffle(IList<Card> cards)
    {
        var n = cards.Count;
        while (n > 1)
        {
            --n;
            var k = _random.Next(n + 1);
            (cards[k], cards[n]) = (cards[n], cards[k]);
        }
    }
}
