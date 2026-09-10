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
[Route("/api/v1/games/{gameId}/")]
[Authorize]
public class GameController(IGameRepository gameRepository,
    IHubContext<GameHub> hubContext, IGameManager gameManager) : ControllerBase
{
    private const string ReceiveMessage = "ReceiveMessage";

    private async Task<(ActionResult?, Game)> BeforePost(string gameId, bool isAction)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return (Unauthorized(), null!);

        var game = await gameRepository.FindGameAsync(Guid.Parse(gameId)).ConfigureAwait(false);

        if (game is null)
            return (NotFound(), null!);

        if (game.IsEnded)
            return (Conflict("Game already ended"), null!);

        if (game.GameState is null)
            return (Conflict("Game is not started"), null!);

        var decompressedGameState = StringCompressor.DecompressString(game.GameState);
        var gameState = JsonConvert.DeserializeObject<GameStateDto>(decompressedGameState)!;

        if (game.PlayerCount != gameState.Players.Count)
            return (Conflict("Wrong number of players"), null!);

        gameManager.SetGameState(gameState);

        if (!isAction && gameManager.CurPlayer.Id != Guid.Parse(userId))
            return (Forbid(), null!);

        return (null, game);
    }

    private async Task AfterPost(CardRc? rc, string gameId, Game game)
    {
        var isEnded = game.IsEnded;

        if (rc is not null)
        {
            isEnded = rc is CardRc.OutlawWin or CardRc.RenegadeWin or CardRc.SheriffWin;
            if (!isEnded && rc is not (CardRc.Ok or CardRc.WaitConditions))
                return;
        }

        var gameStateJson = JsonConvert.SerializeObject(gameManager.GetGameState);
        var compressedGameState = StringCompressor.CompressString(gameStateJson);

        await gameRepository.UpdateGameAsync(Guid.Parse(gameId), compressedGameState, isEnded, game.PlayerCount).ConfigureAwait(false);
        await hubContext.Clients.Group(gameId).SendAsync(ReceiveMessage, gameStateJson).ConfigureAwait(false);
    }

    [HttpPost("playCard")]
    public Task<ActionResult> PlayCard(string gameId, [FromBody] PlayAction playAction)
    {
        ArgumentNullException.ThrowIfNull(playAction);
        return PlayCardInner(gameId, playAction);
    }

    [HttpPost("discardCard")]
    public async Task<ActionResult> DiscardCard(string gameId, [FromBody] string cardId)
    {
        var (result, game) = await BeforePost(gameId, false).ConfigureAwait(false);

        if (result is not null)
            return result;

        try
        {
            gameManager.DiscardCard(Guid.Parse(cardId));
        }
        catch (NotExistingGuidException)
        {
            return NotFound("Card not found");
        }

        await AfterPost(null, gameId, game).ConfigureAwait(false);

        return Ok();
    }

    [HttpPost("endTurn")]
    public async Task<ActionResult> EndTurn(string gameId)
    {
        var (result, game) = await BeforePost(gameId, false).ConfigureAwait(false);

        if (result is not null)
            return result;

        var rc = gameManager.EndTurn();

        await AfterPost(rc, gameId, game).ConfigureAwait(false);

        return Ok(rc);
    }

    [HttpPost("playerAction")]
    public Task<ActionResult> PlayerAction(string gameId, [FromBody] WaitAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return PlayerActionInner(gameId, action);
    }

    private async Task<ActionResult> PlayCardInner(string gameId, [FromBody] PlayAction playAction)
    {
        var (result, game) = await BeforePost(gameId, false).ConfigureAwait(false);

        if (result is not null)
            return result;

        CardRc rc;
        try
        {
            rc = gameManager.PlayCard(playAction.CardId, playAction.TargetPlayerId,
                playAction.TargetCardId);
        }
        catch (NotExistingGuidException)
        {
            return NotFound("Card not found");
        }

        await AfterPost(rc, gameId, game).ConfigureAwait(false);

        return Ok(rc);
    }

    private async Task<ActionResult> PlayerActionInner(string gameId, [FromBody] WaitAction action)
    {
        var (result, game) = await BeforePost(gameId, true).ConfigureAwait(false);

        if (result is not null)
            return result;

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var rc = gameManager.CompleteWait(Guid.Parse(userId!),
            action.TargetPlayerId, action.YesOrNo);

        await AfterPost(rc, gameId, game).ConfigureAwait(false);

        return Ok(rc);
    }
}
