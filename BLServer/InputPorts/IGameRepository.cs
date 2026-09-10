using Server.Models;

namespace Server.InputPorts;

public interface IGameRepository
{
    public Task AddGameAsync(Guid gameId, Guid hostId);
    public Task<Game?> FindGameAsync(Guid gameId);
    public Task DeleteGameAsync(Guid gameId);
    public Task<IEnumerable<Game>> GetIrrelevantGamesAsync(TimeSpan timeSpan);
    public Task DeleteGamesAsync(IEnumerable<Game> games);
    public Task UpdateGameAsync(Guid gameId, string? gameState, bool isEnded, int playerCount);
    public Task<IEnumerable<Game>> GetAllGamesAsync(Guid userId);
}
