using BLComponent;
using BLComponent.InputPorts;
using Serilog;

namespace BLUnitTests;

public class PlayerBuilder
{
    private const PlayerRole PlayerRole = BLComponent.PlayerRole.Outlaw;
    private const int Health = 5;
    private const int MaxHealth = 5;

    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private PlayerRole _playerRole = PlayerRole;
    private int _health = Health;
    private int _maxHealth = MaxHealth;
    private WeaponCard? _weapon;
    private List<Card> _cardsInHand = [];
    private List<Card> _cardsOnBoard = [];
    private bool _isBangPlayed;

    public Player Build()
    {
        var player = new Player(_id, _name, _playerRole,
            _health, _maxHealth, new PLayerCards(_weapon, _cardsInHand, _cardsOnBoard));
        _playerRole = PlayerRole;
        _name = string.Empty;
        _health = Health;
        _maxHealth = MaxHealth;
        _weapon = null;
        _cardsInHand = [];
        _cardsOnBoard = [];
        if (_isBangPlayed)
            player.BangPlayed();
        _isBangPlayed = false;
        return player;
    }

    public PlayerBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public PlayerBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public PlayerBuilder WithPlayerRole(PlayerRole role)
    {
        _playerRole = role;
        return this;
    }

    public PlayerBuilder WithHealth(int health)
    {
        _health = health;
        return this;
    }

    public PlayerBuilder WithMaxHealth(int maxHealth)
    {
        _maxHealth = maxHealth;
        return this;
    }

    public PlayerBuilder WithWeapon(WeaponCard weapon)
    {
        _weapon = weapon;
        return this;
    }

    public PlayerBuilder WithCardsInHand(List<Card> cardsInHand)
    {
        _cardsInHand = cardsInHand;
        return this;
    }

    public PlayerBuilder WithCardsOnBoard(List<Card> cardsOnBoard)
    {
        _cardsOnBoard = cardsOnBoard;
        return this;
    }

    public PlayerBuilder WithBangPlayed()
    {
        _isBangPlayed = true;
        return this;
    }
}

public class PlayerCollectionBuilder
{
    public PlayerCollectionBuilder AddPlayer()
    {
        Players.Add(new PlayerBuilder().Build());
        return this;
    }

    public PlayerCollectionBuilder AddPlayer(Action<PlayerBuilder> configure)
    {
        var builder = new PlayerBuilder();
        configure(builder);
        Players.Add(builder.Build());
        return this;
    }

    public PlayerCollectionBuilder AddPlayers(int playerAmount)
    {
        for (var i = 0; i < playerAmount; ++i)
            AddPlayer();
        return this;
    }

    public List<Player> Players { get; } = [];
}

public class GameManagerBuilder(ICardRepository cardRepository, ILogger logger)
{
    private List<Player> _players = [];
    private Deck _deck = new();
    private Guid _currentPlayerId = Guid.Empty;
    private WaitCondition _waitCondition = new();

    public GameManager Build()
    {
        var gameManager = new GameManager(
            new GameState(_players, _deck, _currentPlayerId, _waitCondition), cardRepository, logger);
        _players = [];
        _deck = new Deck();
        _currentPlayerId = Guid.Empty;
        return gameManager;
    }

    public GameManagerBuilder WithPlayers(List<Player> players)
    {
        _players = players;
        return this;
    }

    public GameManagerBuilder WithDeck(Deck deck)
    {
        _deck = deck;
        return this;
    }

    public GameManagerBuilder WithCurrentPlayerId(Guid currentPlayerId)
    {
        _currentPlayerId = currentPlayerId;
        return this;
    }

    public GameManagerBuilder WithWaitCondition(WaitCondition waitCondition)
    {
        _waitCondition = waitCondition;
        return this;
    }
}
