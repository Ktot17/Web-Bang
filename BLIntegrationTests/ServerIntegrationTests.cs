using System.Net;
using System.Net.Http.Json;
using AutoFixture.Xunit2;
using BLComponent;
using BLComponent.InputPorts;
using BLUnitTests;
using DBComponent.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Server.Models;
using Server.Utils;
using ILogger = Serilog.ILogger;

namespace IntegrationTests;

public class ServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly GameManagerBuilder _gameManagerBuilder;

    public ServerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var dbName = Guid.NewGuid().ToString();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.AddFilter("Microsoft", LogLevel.Error);
            });
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ServerDbContext>));

                if (descriptor != null)
                    services.Remove(descriptor);

                var connString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")?
                                     .Replace("Database=test", $"Database=test_{dbName}") ??
                                 $"Host=localhost;Port=5433;Database=test_{dbName};" +
                                 $"Username=postgres;Password=postgres";

                services.AddDbContext<ServerDbContext>(options =>
                {
                    options.UseNpgsql(connString);
                });
            });
        });

        using var scope = _factory.Services.CreateScope();

        _gameManagerBuilder = new GameManagerBuilder(
            scope.ServiceProvider.GetRequiredService<ICardRepository>(),
            scope.ServiceProvider.GetRequiredService<ILogger>()
            );
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await db.Database.EnsureDeletedAsync();
    }

    private async Task<HttpClient> GetAuthClientAsync(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var token = scope.ServiceProvider.GetRequiredService<ITokenGenerator>()
            .CreateJwtToken(user.Id, user.Name);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return client;
    }

    // AuthController Tests
    [Theory, AutoData]
    public async Task Register_WithValidData_ReturnsOkWithToken(RegisterOrLoginRequest registerRequest)
    {
        // Arrange
        var client = _factory.CreateClient();
        registerRequest = registerRequest with { NeedTwoFactor = false };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/user/register", registerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task Register_WithExistingUsername_ReturnsConflict(RegisterOrLoginRequest registerRequest)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        db.Users.Add(new User(Guid.NewGuid(),
            registerRequest.Username, registerRequest.Email, registerRequest.Password, null));
        await db.SaveChangesAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/user/register", registerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken(RegisterOrLoginRequest registerRequest)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        db.Users.Add(new User(Guid.NewGuid(), registerRequest.Username, registerRequest.Email,
            PasswordHasher.HashPassword(registerRequest.Password), null));
        await db.SaveChangesAsync();
        var client = _factory.CreateClient();
        registerRequest = registerRequest with { NeedTwoFactor = false };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/user/login", registerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task Login_WithInvalidUsername_ReturnsNotFound(RegisterOrLoginRequest loginRequest)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/user/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized(RegisterOrLoginRequest loginRequest)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        db.Users.Add(new User(Guid.NewGuid(), loginRequest.Username, loginRequest.Email,
            PasswordHasher.HashPassword(loginRequest.Password + "1"), null));
        await db.SaveChangesAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/user/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task UpdateUser_WithValidData_ReturnsOk(RegisterOrLoginRequest tUpdateRequest, User user)
    {
        // Arrange
        user.GameId = null;
        user.PasswordHash = PasswordHasher.HashPassword(user.PasswordHash);
        var client = await GetAuthClientAsync(user);
        var updateRequest = tUpdateRequest with { NeedTwoFactor = false };

        // Act
        var response = await client.PatchAsJsonAsync("/api/v1/user/update", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal(updateRequest.Username, updatedUser.Name);
        Assert.True(PasswordHasher.VerifyPassword(updateRequest.Password, updatedUser.PasswordHash));
    }

    [Theory, AutoData]
    public async Task UpdateUser_WithExistingUsername_ReturnsConflict(
        RegisterOrLoginRequest updateRequest,
        User user, User existingUser)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        existingUser.Name = updateRequest.Username;
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        user.GameId = null;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PatchAsJsonAsync("/api/v1/user/update", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task UpdateUser_WhenUserNotFound_ReturnsNotFound(
        RegisterOrLoginRequest updateRequest, User user)
    {
        // Arrange
        var client = await GetAuthClientAsync(user);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        db.Users.RemoveRange(db.Users.Where(u => u.Id == user.Id));
        await db.SaveChangesAsync();

        // Act
        var response = await client.PatchAsJsonAsync("/api/v1/user/update", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task UpdateUser_WithoutAuthentication_ReturnsUnauthorized(
        RegisterOrLoginRequest updateRequest)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PatchAsJsonAsync("/api/v1/user/update", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task DeleteUser_WithAuthenticatedUser_ReturnsOk(User user)
    {
        // Arrange
        user.GameId = null;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.DeleteAsync("/api/v1/user/delete");

        // Assert
        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var deletedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.Null(deletedUser);
    }

    [Theory, AutoData]
    public async Task DeleteUser_WhenUserInGame_ReturnsConflict(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, user.Id, null, false, 1));
        await db.SaveChangesAsync();

        user.GameId = gameId;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.DeleteAsync("/api/v1/user/delete");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task DeleteUser_WhenUserNotFound_ReturnsNotFound(User user)
    {
        // Arrange
        var client = await GetAuthClientAsync(user);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        db.Users.RemoveRange(db.Users.Where(u => u.Id == user.Id));
        await db.SaveChangesAsync();

        // Act
        var response = await client.DeleteAsync("/api/v1/user/delete");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/v1/user/delete");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GamesController Tests
    [Theory, AutoData]
    public async Task CreateGame_WithAuthenticatedUser_ReturnsOk(User user)
    {
        // Arrange
        user.GameId = null;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync("/api/v1/games/", null);

        // Assert
        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var game = await db.Games.FirstOrDefaultAsync(l => l.HostId == user.Id);
        Assert.NotNull(game);
    }

    [Theory, AutoData]
    public async Task CreateGame_WhenUserAlreadyInGame_ReturnsConflict(User user)
    {
        // Arrange
        user.GameId = Guid.NewGuid();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync("/api/v1/games/", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task GetGames_WithAuthenticatedUser_ReturnsGames(User user)
    {
        // Arrange
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.GetAsync("/api/v1/games/");

        // Assert
        response.EnsureSuccessStatusCode();
        var gameResponse = await response.Content.ReadFromJsonAsync<List<Game>>();
        Assert.NotNull(gameResponse);
        Assert.Empty(gameResponse);
    }

    [Theory, AutoData]
    public async Task JoinGame_WithValidGameId_ReturnsOk(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, Guid.NewGuid(), null, false, 1));
        await db.SaveChangesAsync();
        user.GameId = null;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/join", null);

        // Assert
        response.EnsureSuccessStatusCode();
        db.ChangeTracker.Clear();
        var joinUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        var game = await db.Games.FirstAsync(l => l.Id == gameId);
        Assert.Equal(gameId, joinUser.GameId);
        Assert.Equal(2, game.PlayerCount);
    }

    [Theory, AutoData]
    public async Task JoinGame_WithFullGame_ReturnsConflict(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, Guid.NewGuid(), null, false, 7));
        await db.SaveChangesAsync();
        user.GameId = null;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/join", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task JoinGame_WithNonExistentGame_ReturnsNotFound(User user)
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        user.GameId = null;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{nonExistentGameId}/join",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task JoinGame_WhenUserAlreadyInAnotherGame_ReturnsConflict(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var firstGameId = Guid.NewGuid();
        var secondGameId = Guid.NewGuid();

        db.Games.Add(new Game(firstGameId, Guid.NewGuid(), null,
            false, 1));
        db.Games.Add(new Game(secondGameId, Guid.NewGuid(), null,
            false, 1));
        await db.SaveChangesAsync();

        user.GameId = firstGameId;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{secondGameId}/join", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task LeaveGame_WhenUserInGame_ReturnsOk(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, Guid.NewGuid(), null, false, 2));
        await db.SaveChangesAsync();

        user.GameId = gameId;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/leave", null);

        // Assert
        response.EnsureSuccessStatusCode();
        db.ChangeTracker.Clear();
        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Null(updatedUser.GameId);
    }

    [Theory, AutoData]
    public async Task LeaveGame_WhenUserNotInGame_ReturnsConflict(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, Guid.NewGuid(), null, false, 2));
        await db.SaveChangesAsync();

        user.GameId = null;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/leave", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task LeaveGame_WhenUserIsHostAndGameHasMultiplePlayers_DeletesGame(User user,
        User otherUser)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();

        db.Games.Add(new Game(gameId, user.Id, null, false, 2));
        otherUser.GameId = gameId;
        db.Users.Add(otherUser);
        await db.SaveChangesAsync();

        user.GameId = gameId;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/leave", null);

        // Assert
        response.EnsureSuccessStatusCode();
        db.ChangeTracker.Clear();
        var game = await db.Games.FirstOrDefaultAsync(l => l.Id == gameId);
        Assert.Null(game);
        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Null(updatedUser.GameId);
    }

    [Theory, AutoData]
    public async Task StartGame_WithValidGame_ReturnsOk(User user, User user2,
        User user3, User user4, Bang card)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        for (var i = 0; i < 19; ++i)
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO decks.classicdeck (name, suit, rank) VALUES ({card.Name}, {card.Suit}, {card.Rank})"
            );

        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, user.Id, null, false, 4));
        await db.SaveChangesAsync();

        user.GameId = gameId;
        var client = await GetAuthClientAsync(user);

        user2.GameId = gameId;
        user3.GameId = gameId;
        user4.GameId = gameId;
        db.Users.Add(user2);
        db.Users.Add(user3);
        db.Users.Add(user4);
        await db.SaveChangesAsync();

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/start", null);

        // Assert
        response.EnsureSuccessStatusCode();
        db.ChangeTracker.Clear();
        var game = await db.Games.FirstAsync(l => l.Id == gameId);
        Assert.NotNull(game.GameState);
    }

    [Theory, AutoData]
    public async Task StartGame_WithInvalidPlayerCount_ReturnsConflict(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, user.Id, null, false, 2));
        await db.SaveChangesAsync();

        user.GameId = gameId;
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/start", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task StartGame_WithNonExistentGame_ReturnsNotFound(User user)
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{nonExistentGameId}/start",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GameController Tests
    [Theory, GameManagerAutoData(drawPileCount: 2)]
    public async Task PlayCard_WithValidCardAndTurn_ReturnsOk(Deck deck, Stagecoach stagecoach, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        var players = new PlayerCollectionBuilder()
            .AddPlayer(b => b.WithId(user.Id).WithCardsInHand([stagecoach]))
            .AddPlayers(4).Players;
        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithDeck(deck).WithCurrentPlayerId(user.Id).Build().GetGameState;
        var json = JsonConvert.SerializeObject(gameState);
        db.Games.Add(
            new Game(gameId, user.Id, StringCompressor.CompressString(json), false,
                5));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/playCard",
            new PlayAction(stagecoach.Id, null, null));

        // Assert
        response.EnsureSuccessStatusCode();
        db.ChangeTracker.Clear();
        var game = await db.Games.FirstAsync(l => l.Id == gameId);
        Assert.NotNull(game.GameState);
        var newGameState = JsonConvert.DeserializeObject<GameStateDto>(
            StringCompressor.DecompressString(game.GameState)
            );
        Assert.NotNull(newGameState);
        Assert.Equal(user.Id, newGameState.CurrentPlayerId);
        Assert.Equal(2, newGameState.Players.First(p => p.Id == user.Id).CardsInHand.Count);
        Assert.Equal(stagecoach.Id, newGameState.CardDeck.DiscardPile[0].Id);
    }

    [Theory, GameManagerAutoData]
    public async Task PlayCard_WhenNotPlayersTurn_ReturnsForbid(Stagecoach stagecoach, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var players = new PlayerCollectionBuilder()
            .AddPlayer(b => b.WithId(otherId).WithCardsInHand([stagecoach]))
            .AddPlayers(4).Players;
        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithCurrentPlayerId(otherId).Build().GetGameState;
        var json = JsonConvert.SerializeObject(gameState);
        db.Games.Add(
            new Game(gameId, user.Id, StringCompressor.CompressString(json),
                false, 5));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/playCard",
            new PlayAction(stagecoach.Id, null, null));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task PlayCard_WithNonExistentGame_ReturnsNotFound(User user)
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{nonExistentGameId}/playCard",
            new PlayAction(Guid.Empty, null, null));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory, GameManagerAutoData]
    public async Task PlayCard_WithInvalidCardId_ReturnsNotFound(Deck deck, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();

        var players = new PlayerCollectionBuilder()
            .AddPlayer(b => b.WithId(user.Id))
            .AddPlayers(4).Players;

        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithDeck(deck).WithCurrentPlayerId(user.Id).Build().GetGameState;
        var json = JsonConvert.SerializeObject(gameState);

        db.Games.Add(new Game(gameId, user.Id,
            StringCompressor.CompressString(json), false, 5));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/playCard",
            new PlayAction(Guid.Empty, null, null));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory, GameManagerAutoData]
    public async Task PlayCard_WhenGameNotStarted_ReturnsConflict(Stagecoach stagecoach, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, user.Id, null, false, 4));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/playCard",
            new PlayAction(stagecoach.Id, null, null));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, GameManagerAutoData]
    public async Task DiscardCard_WithValidCard_ReturnsOk(Deck deck, Stagecoach stagecoach, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        var players = new PlayerCollectionBuilder()
            .AddPlayer(b => b.WithId(user.Id).WithCardsInHand([stagecoach]))
            .AddPlayers(4).Players;
        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithDeck(deck).WithCurrentPlayerId(user.Id).Build().GetGameState;
        var json = JsonConvert.SerializeObject(gameState);
        db.Games.Add(
            new Game(gameId, user.Id, StringCompressor.CompressString(json), false,
                5));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/discardCard",
            stagecoach.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        db.ChangeTracker.Clear();
        var game = await db.Games.FirstAsync(l => l.Id == gameId);
        Assert.NotNull(game.GameState);
        var newGameState = JsonConvert.DeserializeObject<GameStateDto>(
            StringCompressor.DecompressString(game.GameState)
            );
        Assert.NotNull(newGameState);
        Assert.Equal(user.Id, newGameState.CurrentPlayerId);
        Assert.Empty(newGameState.Players.First(p => p.Id == user.Id).CardsInHand);
        Assert.Equal(stagecoach.Id, newGameState.CardDeck.DiscardPile[0].Id);
    }

    [Theory, GameManagerAutoData]
    public async Task DiscardCard_WithInvalidCardId_ReturnsNotFound(Deck deck, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        var invalidCardId = Guid.NewGuid();

        var players = new PlayerCollectionBuilder()
            .AddPlayer(b => b.WithId(user.Id))
            .AddPlayers(4).Players;

        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithDeck(deck).WithCurrentPlayerId(user.Id).Build().GetGameState;
        var json = JsonConvert.SerializeObject(gameState);

        db.Games.Add(new Game(gameId, user.Id, StringCompressor.CompressString(json), false, 5));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/discardCard",
            invalidCardId.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory, GameManagerAutoData]
    public async Task DiscardCard_WhenGameNotStarted_ReturnsConflict(Stagecoach stagecoach, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, user.Id, null, false, 4));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/games/{gameId}/discardCard",
            stagecoach.Id.ToString());

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory, GameManagerAutoData(drawPileCount: 2)]
    public async Task EndTurn_WithValidTurn_ReturnsOk(Deck deck, User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        var nextPlayerId = Guid.NewGuid();
        var players = new PlayerCollectionBuilder()
            .AddPlayer(b => b.WithId(user.Id))
            .AddPlayer(b => b.WithId(nextPlayerId))
            .AddPlayers(4).Players;
        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithDeck(deck).WithCurrentPlayerId(user.Id).Build().GetGameState;
        var gameStateJson = JsonConvert.SerializeObject(gameState);
        db.Games.Add(
            new Game(gameId, user.Id, StringCompressor.CompressString(gameStateJson), false,
                6));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/endTurn", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        db.ChangeTracker.Clear();
        var game = await db.Games.FirstAsync(l => l.Id == gameId);
        Assert.NotNull(game.GameState);
        var newGameState = JsonConvert.DeserializeObject<GameStateDto>(
            StringCompressor.DecompressString(game.GameState)
            );
        Assert.NotNull(newGameState);
        Assert.Equal(nextPlayerId, newGameState.CurrentPlayerId);
        Assert.Equal(2, newGameState.Players.First(p => p.Id == nextPlayerId).CardsInHand.Count);
    }

    [Theory, AutoData]
    public async Task EndTurn_WhenNotPlayersTurn_ReturnsForbid(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        var nextPlayerId = Guid.NewGuid();
        var players = new PlayerCollectionBuilder()
            .AddPlayer(b => b.WithId(user.Id))
            .AddPlayer(b => b.WithId(nextPlayerId))
            .AddPlayers(4).Players;
        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithCurrentPlayerId(nextPlayerId).Build().GetGameState;
        var json = JsonConvert.SerializeObject(gameState);
        db.Games.Add(
            new Game(gameId, user.Id, StringCompressor.CompressString(json), false,
                6));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/endTurn", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory, AutoData]
    public async Task EndTurn_WhenGameNotStarted_ReturnsConflict(User user)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        db.Games.Add(new Game(gameId, user.Id, null, false, 4));
        await db.SaveChangesAsync();
        var client = await GetAuthClientAsync(user);

        // Act
        var response = await client.PostAsync($"/api/v1/games/{gameId}/endTurn", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
