using BLComponent;
using MongoDB.Driver;
using Newtonsoft.Json;
using Server.InputPorts;
using Server.Models;

namespace DBComponent.MongoDb;

public class GameRepository(ServerDbContext context) : IGameRepository
{
    private readonly IMongoCollection<Game> _games = context.Games;

    public async Task AddGameAsync(Guid gameId, Guid hostId)
    {
        var game = new Game(gameId, hostId, null, false, 1);
        await _games.InsertOneAsync(game).ConfigureAwait(false);
    }

    public async Task<Game?> FindGameAsync(Guid gameId) =>
        await _games.Find(g => g.Id == gameId).FirstOrDefaultAsync().ConfigureAwait(false);

    public async Task DeleteGameAsync(Guid gameId)
    {
        var result = await _games.DeleteOneAsync(g => g.Id == gameId).ConfigureAwait(false);
        if (result.DeletedCount == 0)
            throw new EntityNotFoundException(typeof(Game));
    }

    public async Task<IEnumerable<Game>> GetIrrelevantGamesAsync(TimeSpan timeSpan)
    {
        var cutoff = DateTimeOffset.UtcNow.Add(-timeSpan).DateTime;
        return await _games.Find(g => g.LastUpdated < cutoff).ToListAsync().ConfigureAwait(false);
    }

    public async Task UpdateGameAsync(Guid gameId, string? gameState, bool isEnded, int playerCount)
    {
        var update = Builders<Game>.Update
            .Set(g => g.GameState, gameState)
            .Set(g => g.IsEnded, isEnded)
            .Set(g => g.PlayerCount, playerCount)
            .Set(g => g.LastUpdated, DateTimeOffset.UtcNow.DateTime);

        var result = await _games.UpdateOneAsync(g => g.Id == gameId, update).ConfigureAwait(false);
        if (result.MatchedCount == 0)
            throw new EntityNotFoundException(typeof(Game));
    }

    public async Task<IEnumerable<Game>> GetAllGamesAsync(Guid userId)
    {
        var allGames = await _games.Find(g => !g.IsEnded && g.PlayerCount < GameManager.MaxPlayersCount)
                                   .ToListAsync().ConfigureAwait(false);

        return allGames.Where(g => g.GameState == null
            || JsonConvert.DeserializeObject<GameStateDto>(
                StringCompressor.DecompressString(g.GameState))!.Players.Any(p => p.Id == userId));
    }

    public async Task DeleteGamesAsync(IEnumerable<Game> games)
    {
        var ids = games.Select(g => g.Id).ToList();
        await _games.DeleteManyAsync(g => ids.Contains(g.Id)).ConfigureAwait(false);
    }
}
