namespace BLComponent.OutputPort;

public interface IGameManager
{
    public void GameInit(IDictionary<Guid, string> users);
    public void GameStart();
    public CardRc CompleteWait(Guid playerId, Guid? cardId, bool? yesOrNo);
    public CardRc PlayCard(Guid cardId, Guid? targetPlayerId, Guid? targetCardId);
    public void DiscardCard(Guid cardId);
    public CardRc EndTurn();
    public Player CurPlayer { get; }
    public IReadOnlyList<Player> Players { get; }
    public IReadOnlyList<Player> DeadPlayers { get; }
    public Card? TopDiscardedCard { get; }
    public IReadOnlyList<Card> CardsInDeck { get; }
    public int GetRange(Guid playerId, Guid targetId);
    public IList<Card> GetAllCards { get; }
    public GameStateDto GetGameState { get; }
    public void SetGameState(GameStateDto gameState);
}
