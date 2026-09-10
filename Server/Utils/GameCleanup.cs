using Server.InputPorts;

namespace Server.Utils;

public class GameCleanup(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken).ConfigureAwait(false);

            using var scope = serviceScopeFactory.CreateScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var gameRepository = scope.ServiceProvider.GetRequiredService<IGameRepository>();

            var games = (await gameRepository.GetIrrelevantGamesAsync(TimeSpan.FromDays(1)).ConfigureAwait(false)).ToList();
            foreach (var gameId in games.Select(game => game.Id))
                await userRepository.ClearGameAsync(gameId).ConfigureAwait(false);
            await gameRepository.DeleteGamesAsync(games).ConfigureAwait(false);
        }
    }
}
