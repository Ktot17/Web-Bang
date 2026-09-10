using System.Security.Claims;
using BLComponent;
using BLComponent.OutputPort;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Server.InputPorts;
using Server.Models;
using Server.Utils;

namespace Server.Controllers;

[ApiController]
[Route("/api/v1/games/")]
[Authorize]
public class GamesController(IUserRepository userRepository,
    IGameRepository gameRepository, IGameManager gameManager,
    IHubContext<GameHub> hubContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> CreateGame()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return Unauthorized();

        var user = await userRepository.FindUserAsync(Guid.Parse(userId)).ConfigureAwait(false);

        if (user is null)
            return NotFound();

        if (user.GameId is not null)
            return Conflict();

        var gameId = Guid.NewGuid();
        user.GameId = gameId;
        await gameRepository.AddGameAsync(gameId, Guid.Parse(userId)).ConfigureAwait(false);
        await userRepository.UpdateUserAsync(user).ConfigureAwait(false);
        await GameHub.AddToGroupByUserId(hubContext, userId, gameId.ToString()).ConfigureAwait(false);

        return Created();
    }

    [HttpGet]
    public async Task<ActionResult<List<Game>>> GetGames()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return Unauthorized();

        var games = await gameRepository.GetAllGamesAsync(Guid.Parse(userId)).ConfigureAwait(false);

        return Ok(games.ToList());
    }

    [HttpPost("{gameId}/join")]
    public async Task<ActionResult> JoinGame(string gameId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return Unauthorized();

        var game = await gameRepository.FindGameAsync(Guid.Parse(gameId)).ConfigureAwait(false);

        if (game is null)
            return NotFound("Game not found");

        if (game.PlayerCount >= GameManager.MaxPlayersCount)
            return Conflict("Full game");

        var user = await userRepository.FindUserAsync(Guid.Parse(userId)).ConfigureAwait(false);

        if (user is null)
            return NotFound("User not found");

        if (game.GameState is not null &&
            JsonConvert.DeserializeObject<GameStateDto>(
                StringCompressor.DecompressString(game.GameState)
                )!.Players.All(p => p.Id != user.Id))
            return Conflict("Game started and you are not a part of it");

        if (user.GameId is not null)
            return Conflict("You have already joined the game");

        user.GameId = game.Id;
        await gameRepository.UpdateGameAsync(Guid.Parse(gameId), game.GameState, game.IsEnded, game.PlayerCount + 1).ConfigureAwait(false);
        await userRepository.UpdateUserAsync(user).ConfigureAwait(false);
        await GameHub.AddToGroupByUserId(hubContext, userId, gameId).ConfigureAwait(false);

        await hubContext.Clients.Group(gameId).SendAsync("ReceiveMessage", $"{user.Name} joined").ConfigureAwait(false);

        return Ok();
    }

    [HttpPost("{gameId}/leave")]
    public async Task<ActionResult> LeaveGame(string gameId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return Unauthorized();

        var game = await gameRepository.FindGameAsync(Guid.Parse(gameId)).ConfigureAwait(false);

        if (game is null)
            return NotFound("Game not found");

        var user = await userRepository.FindUserAsync(Guid.Parse(userId)).ConfigureAwait(false);

        if (user is null)
            return NotFound("User not found");

        if (user.GameId is null)
            return Conflict();

        await hubContext.Clients.Group(gameId).SendAsync("ReceiveMessage", $"{user.Name} left").ConfigureAwait(false);

        if (game.HostId == user.Id && game.GameState is null)
        {
            foreach (var userInGame in await userRepository.FindUsersByGameIdAsync(Guid.Parse(gameId)).ConfigureAwait(false))
                await GameHub.RemoveFromGroupByUserId(hubContext, userInGame.Id.ToString(), gameId).ConfigureAwait(false);
            await userRepository.ClearGameAsync(Guid.Parse(gameId)).ConfigureAwait(false);
            await gameRepository.DeleteGameAsync(Guid.Parse(gameId)).ConfigureAwait(false);
        }
        else
        {
            user.GameId = null;
            await userRepository.UpdateUserAsync(user).ConfigureAwait(false);
            await gameRepository.UpdateGameAsync(
                Guid.Parse(gameId), game.GameState, game.IsEnded, game.PlayerCount - 1).ConfigureAwait(false);
            await GameHub.RemoveFromGroupByUserId(hubContext, userId, gameId).ConfigureAwait(false);
        }

        return Ok();
    }

    [HttpPost("{gameId}/start")]
    public async Task<ActionResult> StartGame(string gameId)
    {
        var game = await gameRepository.FindGameAsync(Guid.Parse(gameId)).ConfigureAwait(false);

        if (game is null)
            return NotFound();

        if (game.PlayerCount > GameManager.MaxPlayersCount || game.PlayerCount < GameManager.MinPlayersCount)
            return Conflict("Wrong number of players to start the game");

        string gameStateJson;

        if (game.GameState is null)
        {
            var usersEnumerable = await userRepository.FindUsersByGameIdAsync(Guid.Parse(gameId)).ConfigureAwait(false);
            var users = usersEnumerable.ToList();
            var userDictionary = users.Select(u => u.Id).Zip(users.Select(u => u.Name))
                .ToDictionary(pair => pair.First, pair => pair.Second);

            gameManager.GameInit(userDictionary);
            gameManager.GameStart();
            gameStateJson = JsonConvert.SerializeObject(gameManager.GetGameState);
            var compressedGameState = StringCompressor.CompressString(gameStateJson);

            await gameRepository.UpdateGameAsync(game.Id, compressedGameState, game.IsEnded, game.PlayerCount).ConfigureAwait(false);
        }
        else
        {
            var gameState = JsonConvert.DeserializeObject<GameStateDto>(
                StringCompressor.DecompressString(game.GameState))!;
            if (gameState.Players.Count != game.PlayerCount)
                return Conflict("Wrong number of players to resume the game");

            gameStateJson = game.GameState;
        }

        await hubContext.Clients.Group(gameId).SendAsync("ReceiveMessage", gameStateJson).ConfigureAwait(false);

        return Ok();
    }
}
