using Newtonsoft.Json;

namespace BLComponent;

public record PlayerDto
{
    [JsonProperty]
    public Guid Id { get; set; }

    [JsonProperty]
    public string Name { get; set; } = string.Empty;

    [JsonProperty]
    public PlayerRole Role { get; set; }

    [JsonProperty]
    public int Health { get; set; }

    [JsonProperty]
    public int MaxHealth { get; set; }

    [JsonProperty]
    public WeaponCardDto? Weapon { get; set; } = new();

    [JsonProperty]
    public IReadOnlyList<CardDto> CardsInHand { get; set; } = [];

    [JsonProperty]
    public IReadOnlyList<CardDto> CardsOnBoard { get; set; } = [];

    [JsonProperty]
    public bool IsBangPlayed { get; set; }

    [JsonProperty]
    public bool IsDeadOnThisTurn { get; set; }

    public PlayerDto() { }

    internal PlayerDto(Player p)
    {
        Id = p.Id;
        Name = p.Name;
        Role = p.Role;
        Health = p.Health;
        MaxHealth = p.MaxHealth;
        Weapon = p.Weapon is null ? null : new WeaponCardDto(p.Weapon);
        CardsInHand = p.CardsInHand.Select(x => new CardDto(x)).ToList();
        CardsOnBoard = p.CardsOnBoard.Select(x => new CardDto(x)).ToList();
        IsBangPlayed = p.IsBangPlayed;
        IsDeadOnThisTurn = p.IsDeadOnThisTurn;
    }
}

internal record PLayerCards(WeaponCard? Weapon, List<Card> CardsInHand, List<Card> CardsOnBoard);

public class Player
{
    private readonly List<Card> _cardsInHand = [];
    private readonly List<Card> _cardsOnBoard = [];

    public Guid Id { get; }
    public string Name { get; }
    public PlayerRole Role { get; }
    public int Health { get; private set; }
    public int MaxHealth { get; }
    public WeaponCard? Weapon { get; private set; }
    public IReadOnlyList<Card> CardsInHand => _cardsInHand;
    public IReadOnlyList<Card> CardsOnBoard => _cardsOnBoard;
    public bool IsBangPlayed { get; private set; }
    public bool IsDead => Health <= 0;
    public int Range => Weapon?.Range ?? 1;
    public bool IsDeadOnThisTurn { get; private set; }
    public int CardCount => _cardsInHand.Count + _cardsOnBoard.Count + (Weapon is null ? 0 : 1);

    public Player(Guid id, string name, PlayerRole role, int maxHealth)
    {
        Id = id;
        Name = name;
        Role = role;
        Health = maxHealth;
        MaxHealth = maxHealth;
    }

    internal Player(
        Guid id,
        string name,
        PlayerRole role,
        int health,
        int maxHealth,
        PLayerCards cards)
    {
        Id = id;
        Name = name;
        Role = role;
        Health = health;
        MaxHealth = maxHealth;
        Weapon = cards.Weapon;
        _cardsInHand = cards.CardsInHand;
        _cardsOnBoard = cards.CardsOnBoard;
    }

    internal Player(PlayerDto dto)
    {
        Id = dto.Id;
        Name = dto.Name;
        Role = dto.Role;
        Health = dto.Health;
        MaxHealth = dto.MaxHealth;
        if (dto.Weapon is not null)
            Weapon = (WeaponCard)CardFactory.CreateCard(
                dto.Weapon.Id,
                dto.Weapon.Name,
                dto.Weapon.Suit,
                dto.Weapon.Rank,
                null);
        foreach (var cardDto in dto.CardsInHand)
            _cardsInHand.Add(CardFactory.CreateCard(
                cardDto.Id,
                cardDto.Name,
                cardDto.Suit,
                cardDto.Rank,
                cardDto.Options));
        foreach (var cardDto in dto.CardsOnBoard)
            _cardsOnBoard.Add(CardFactory.CreateCard(
                cardDto.Id,
                cardDto.Name,
                cardDto.Suit,
                cardDto.Rank,
                cardDto.Options));
        IsBangPlayed = dto.IsBangPlayed;
        IsDeadOnThisTurn = dto.IsDeadOnThisTurn;
    }

    internal void AddCardInHand(Card card)
    {
        _cardsInHand.Add(card);
    }

    internal void AddCardOnBoard(Card card)
    {
        _cardsOnBoard.Add(card);
    }

    internal WeaponCard? ChangeWeapon(WeaponCard weapon)
    {
        var removedWeapon = Weapon;
        Weapon = weapon;
        return removedWeapon;
    }

    internal Card RemoveCard(Guid cardId)
    {
        Card? removedCard;
        if ((removedCard = _cardsInHand.Find(x => x.Id == cardId)) is not null)
            _cardsInHand.Remove(removedCard);
        else if ((removedCard = _cardsOnBoard.Find(x => x.Id == cardId)) is not null)
            _cardsOnBoard.Remove(removedCard);
        else if (Weapon is not null && cardId == Weapon.Id)
        {
            removedCard = Weapon;
            Weapon = null;
        }
        else
            throw new NotExistingGuidException();
        return removedCard;
    }

    internal void DeadEarlier() =>
        IsDeadOnThisTurn = false;

    internal void BangPlayed() =>
        IsBangPlayed = true;

    internal void EndTurn() =>
        IsBangPlayed = false;

    internal bool ApplyDamage(int damage, GameState state)
    {
        Health -= damage;
        if (Health > 0)
            return true;
        while (Health != 1)
        {
            var card = _cardsInHand.Find(x => x.Name == CardName.Beer);
            if (card is null)
            {
                IsDeadOnThisTurn = true;
                return false;
            }

            _cardsInHand.Remove(card);
            if (card.Play(state, null, null) is CardRc.Ok)
                state.CardDeck.Discard(card);
            else
            {
                AddCardInHand(card);
                IsDeadOnThisTurn = true;
                return false;
            }
        }
        return true;
    }

    internal bool Heal(int healAmount)
    {
        Health += healAmount;
        var rc = Health > MaxHealth;
        if (rc)
            Health = MaxHealth;
        return rc;
    }
}
