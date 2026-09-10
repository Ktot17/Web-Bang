using BLComponent;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Server.InputPorts;
using Server.Models;

namespace DBComponent.MySql;

public class GameRepository(ServerDbContext context) : IGameRepository
{
    public void AddGame(Guid gameId, Guid hostId)
    {
        var game = new Game(gameId, hostId, null, false, 1);
        context.Games.Add(game);
        context.SaveChanges();
    }

    public Game? FindGame(Guid gameId) =>
        context.Games.AsNoTracking().FirstOrDefault(game => game.Id == gameId);

    public void DeleteGame(Guid gameId)
    {
        var game = FindGame(gameId);
        if (game == null)
            throw new EntityNotFoundException(typeof(Game));
        context.Games.Remove(game);
        context.SaveChanges();
    }

    public IEnumerable<Game> GetIrrelevantGames(TimeSpan timeSpan)
    {
        var lastUpdated = DateTimeOffset.UtcNow.Add(-timeSpan).DateTime;
        return context.Games
            .Where(l => l.LastUpdated < lastUpdated)
            .AsNoTracking().ToList();
    }

    public void DeleteGames(IEnumerable<Game> games)
    {
        context.Games.RemoveRange(games);
        context.SaveChanges();
    }

    public void UpdateGame(Guid gameId, string? gameState, bool isEnded, int playerCount)
    {
        var game = FindGame(gameId);
        if (game == null)
            throw new EntityNotFoundException(typeof(Game));
        game.GameState = gameState;
        game.IsEnded = isEnded;
        game.PlayerCount = playerCount;
        game.LastUpdated = DateTimeOffset.UtcNow.DateTime;
        context.Games.Update(game);
        context.SaveChanges();
    }

    public IEnumerable<Game> GetAllGames(Guid userId)
    {
        var allGames = context.Games
            .Where(g => !g.IsEnded
                        && g.PlayerCount < GameManager.MaxPlayersCount)
            .AsNoTracking().ToList();
        return allGames.Where(g => g.GameState == null
                                   || JsonConvert
                                       .DeserializeObject<GameStateDto>(
                                           StringCompressor.DecompressString(g.GameState))!
                                       .Players.Select(p => p.Id).Contains(userId));
    }

    public async Task AddGameAsync(Guid gameId, Guid hostId)
    {
        var game = new Game(gameId, hostId, null, false, 1);
        await context.Games.AddAsync(game).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<Game?> FindGameAsync(Guid gameId) =>
        await context.Games.AsNoTracking().FirstOrDefaultAsync(game => game.Id == gameId).ConfigureAwait(false);

    public async Task DeleteGameAsync(Guid gameId)
    {
        var game = await context.Games.FindAsync(gameId).ConfigureAwait(false);
        if (game == null)
            throw new EntityNotFoundException(typeof(Game));
        context.Games.Remove(game);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<Game>> GetIrrelevantGamesAsync(TimeSpan timeSpan)
    {
        var lastUpdated = DateTimeOffset.UtcNow.Add(-timeSpan).DateTime;
        return await context.Games
            .Where(l => l.LastUpdated < lastUpdated)
            .AsNoTracking().ToListAsync().ConfigureAwait(false);
    }


    public async Task DeleteGamesAsync(IEnumerable<Game> games)
    {
        context.Games.RemoveRange(games);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task UpdateGameAsync(Guid gameId, string? gameState, bool isEnded, int playerCount)
    {
        var game = await FindGameAsync(gameId).ConfigureAwait(false);
        if (game == null)
            throw new EntityNotFoundException(typeof(Game));
        game.GameState = gameState;
        game.IsEnded = isEnded;
        game.PlayerCount = playerCount;
        game.LastUpdated = DateTimeOffset.UtcNow.DateTime;
        context.Games.Update(game);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<Game>> GetAllGamesAsync(Guid userId)
    {
        var allGames = await context.Games
            .Where(g => !g.IsEnded
                        && g.PlayerCount < GameManager.MaxPlayersCount)
            .AsNoTracking().ToListAsync().ConfigureAwait(false);
        return allGames.Where(g => g.GameState == null
                                   || JsonConvert
                                       .DeserializeObject<GameStateDto>(
                                           StringCompressor.DecompressString(g.GameState))!
                                       .Players.Select(p => p.Id).Contains(userId));
    }
}
