using System.Net.Http.Json;
using System.Threading.Channels;
using BLComponent;
using BLComponent.InputPorts;
using BLUnitTests;
using DBComponent.Postgres;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Server.Models;
using Server.Utils;

namespace E2ETests;

public class E2ETests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly GameManagerBuilder _gameManagerBuilder;

    private readonly List<User> _users = [];
    private readonly List<string> _passwords = [];
    private Deck _deck = null!;
    private Guid _bangId;
    private Guid _generalStoreId;
    private List<Guid> _cards = null!;
    private HubConnection _connection = null!;
    private const int PlayerCount = 4;

    private readonly Dictionary<string, (Dictionary<string, string?>, IEmailReceiver)> _emailHandle = new()
    {
        ["mock"] = (new Dictionary<string, string?>
        {
            ["SMTP:Host"] = "localhost",
            ["SMTP:Port"] = "1025",
            ["SMTP:UseDefaultCredentials"] = "true",
            ["SMTP:EnableSsl"] = "false"
        }, new MailHogReceiver(new Uri("http://localhost:8025/"))),
    };

    public E2ETests(WebApplicationFactory<Program> factory)
    {
        var dbName = Guid.NewGuid().ToString();
        _factory = factory.WithWebHostBuilder(builder =>
        {
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

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        _emailHandle["real"] = (new Dictionary<string, string?>
        {
            ["SMTP:Host"] = "smtp.imitate.email",
            ["SMTP:Port"] = "587",
            ["SMTP:User"] = config["Smtp:ImitateUser"]!,
            ["SMTP:Password"] = config["Smtp:ImitatePassword"]!,
        }, new ImitateEmailReceiver(
            config["Smtp:ImitateUser"]!,
            config["Smtp:ImitatePassword"]!));
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await db.Database.EnsureCreatedAsync();
        var sqlScript = await File.ReadAllTextAsync(
            Path.Combine(Directory.GetCurrentDirectory(),
                "..", "..", "..", "FillClassicDeck.sql"));
        await db.Database.ExecuteSqlRawAsync(sqlScript);
        await db.SaveChangesAsync();
        var client = _factory.CreateClient();
        var serverUri = client.BaseAddress!.ToString().TrimEnd('/');

        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUri}/api/v1/hub", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        await _connection.StartAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await db.Database.EnsureDeletedAsync();
        await _connection.DisposeAsync();
    }

    private async Task PrepareDb()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var gameId = Guid.NewGuid();
        _bangId = Guid.NewGuid();
        _generalStoreId = Guid.NewGuid();
        var drawPile = new List<Card>();
        for (var i = 0; i < 15; i++)
            drawPile.Add(CardFactory.CreateCard(CardName.Bang, CardSuit.Clubs, CardRank.Seven));
        _deck = new Deck(drawPile, []);
        for (var i = 0; i < PlayerCount; ++i)
        {
            var password = Guid.NewGuid().ToString();
            var user = new User(Guid.NewGuid(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid() + "@example.com",
                PasswordHasher.HashPassword(password),
                null);
            db.Users.Add(user);
            _users.Add(user);
            _passwords.Add(password);
        }
        var players = new PlayerCollectionBuilder()
            .AddPlayer(p => p.WithId(_users[0].Id)
                .WithHealth(1).WithPlayerRole(PlayerRole.Sheriff)
                .WithCardsInHand([CardFactory.CreateCard(CardName.Missed,
                    CardSuit.Clubs, CardRank.Ace)]))
            .AddPlayer(p => p.WithId(_users[1].Id)
                .WithCardsInHand([CardFactory.CreateCard(_bangId, CardName.Bang,
                        CardSuit.Clubs, CardRank.Ace, null),
                    CardFactory.CreateCard(_generalStoreId, CardName.GeneralStore,
                        CardSuit.Clubs, CardRank.Ace, null)]))
            .AddPlayer(p => p.WithId(_users[2].Id)
                .WithPlayerRole(PlayerRole.Renegade))
            .AddPlayer(p => p.WithId(_users[3].Id)).Players;
        var gameState = _gameManagerBuilder
            .WithPlayers(players).WithDeck(_deck)
            .WithCurrentPlayerId(_users[0].Id).Build().GetGameState;
        _cards = _deck.TopCards(PlayerCount + 2).Skip(2).Select(c => c.Id).ToList();
        var game = new Game(gameId, _users[0].Id,
            StringCompressor.CompressString(JsonConvert.SerializeObject(gameState)),
            false, 0);
        db.Games.Add(game);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task E2E_CreateTest()
    {
        var clients = new List<HttpClient>();
        var userNames = new List<string>();
        var userEmails = new List<string>();

        for (var i = 0; i < PlayerCount; ++i)
        {
            clients.Add(_factory.CreateClient());
            userNames.Add(Guid.NewGuid().ToString());
            userEmails.Add(Guid.NewGuid() + "@example.com");

            var response = await clients[i].PostAsJsonAsync("/api/v1/user/register",
                new RegisterOrLoginRequest(userNames[i], userEmails[i],
                    Guid.NewGuid().ToString(), false));
            response.EnsureSuccessStatusCode();

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            clients[i].DefaultRequestHeaders.Add("Authorization", $"Bearer " + auth!.Token);
        }

        var stateChannels = new List<Channel<string>>();

        for (var i = 0; i < PlayerCount; ++i)
        {
            var token = clients[i].DefaultRequestHeaders.Authorization!.Parameter;
            var serverUri = clients[i].BaseAddress!.ToString().TrimEnd('/');

            var channel = Channel.CreateUnbounded<string>();
            stateChannels.Add(channel);

            var connection = new HubConnectionBuilder()
                .WithUrl($"{serverUri}/api/v1/hub", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .Build();

            connection.On<string>("ReceiveMessage", msg =>
            {
                if (msg.Contains("Players"))
                    channel.Writer.TryWrite(msg);
            });

            await connection.StartAsync();
        }

        var create = await clients[0].PostAsync("/api/v1/games", null);
        create.EnsureSuccessStatusCode();

        var games = await clients[0].GetFromJsonAsync<List<Game>>("/api/v1/games");
        var game = games![0];

        for (var i = 1; i < PlayerCount; ++i)
        {
            var resp = await clients[i].PostAsync(
                $"/api/v1/games/{game.Id}/join", null);
            resp.EnsureSuccessStatusCode();
        }

        var start = await clients[0].PostAsync(
            $"/api/v1/games/{game.Id}/start", null);
        start.EnsureSuccessStatusCode();

        var state = new GameStateDto();
        for (var i = 0; i < PlayerCount; ++i)
        {
            state = await WaitForState(i);
            Assert.NotEmpty(state.Players);
        }

        var userIndexes = state.Players
            .Select(p => userNames.FindIndex(u => u == p.Name))
            .ToList();

        var j = 0;

        foreach (var i in userIndexes)
        {
            var currentPlayer = state.Players[j];

            var bang = currentPlayer.CardsInHand.FirstOrDefault(c => c.Name == CardName.Bang);
            HttpResponseMessage response;

            if (bang is null)
            {
                response = await clients[i].PostAsJsonAsync(
                    $"/api/v1/games/{game.Id}/discardCard",
                    currentPlayer.CardsInHand[0].Id.ToString());
            }
            else
            {
                var targetIndex = (j + 1) % PlayerCount;
                var targetId = state.Players[targetIndex].Id;

                response = await clients[i].PostAsJsonAsync(
                    $"/api/v1/games/{game.Id}/playCard",
                    new PlayAction(bang.Id, targetId, null));
                response.EnsureSuccessStatusCode();
                state = await WaitForState(0);
                Assert.NotEmpty(state.Players);

                var targetClientIndex = userNames.FindIndex(u =>
                    u == state.Players[targetIndex].Name);
                response = await clients[targetClientIndex].PostAsJsonAsync(
                    $"/api/v1/games/{game.Id}/playerAction",
                    new WaitAction(null, false));
            }

            response.EnsureSuccessStatusCode();

            state = await WaitForState(0);
            Assert.NotEmpty(state.Players);

            for (var k = 0; k < 2; ++k)
            {
                response = await clients[i].PostAsJsonAsync(
                    $"/api/v1/games/{game.Id}/discardCard",
                    state.Players[j].CardsInHand[0].Id.ToString());
                response.EnsureSuccessStatusCode();
                state = await WaitForState(0);
            }

            response = await clients[i].PostAsync(
                $"/api/v1/games/{game.Id}/endTurn", null);
            response.EnsureSuccessStatusCode();

            state = await WaitForState(0);

            j++;
        }

        return;

        async Task<GameStateDto> WaitForState(int playerIndex)
        {
            var timeout = TimeSpan.FromSeconds(5);
            using var cts = new CancellationTokenSource(timeout);

            var firstJson = await stateChannels[playerIndex].Reader.ReadAsync(cts.Token);
            var lastJson = firstJson;

            while (stateChannels[playerIndex].Reader.TryRead(out var nextJson))
            {
                lastJson = nextJson;
            }

            return JsonConvert.DeserializeObject<GameStateDto>(lastJson)!;
        }
    }

    [Fact]
    public async Task E2E_SaveTest()
    {
        await PrepareDb();
        var clients = new List<HttpClient>();
        HttpResponseMessage response;

        for (var i = 0; i < PlayerCount; ++i)
        {
            clients.Add(_factory.CreateClient());
            response = await clients[i].PostAsJsonAsync("/api/v1/user/login",
                new RegisterOrLoginRequest(_users[i].Name, _users[i].Email,
                    _passwords[i], false));
            response.EnsureSuccessStatusCode();
            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            clients[i].DefaultRequestHeaders.Add("Authorization", $"Bearer {authResponse!.Token}");
        }

        response = await clients[1].GetAsync("api/v1/games");
        response.EnsureSuccessStatusCode();
        var gameResponse = await response.Content.ReadFromJsonAsync<List<Game>>();
        var game = gameResponse![0];

        response = await clients[2].PostAsync($"/api/v1/games/{game.Id}/join", null);
        response.EnsureSuccessStatusCode();
        response = await clients[1].PostAsync($"/api/v1/games/{game.Id}/join", null);
        response.EnsureSuccessStatusCode();
        response = await clients[0].PostAsync($"/api/v1/games/{game.Id}/join", null);
        response.EnsureSuccessStatusCode();
        response = await clients[3].PostAsync($"/api/v1/games/{game.Id}/join", null);
        response.EnsureSuccessStatusCode();

        response = await clients[0].PostAsync($"/api/v1/games/{game.Id}/start", null);
        response.EnsureSuccessStatusCode();

        response = await clients[0].PostAsync($"/api/v1/games/{game.Id}/endTurn", null);
        response.EnsureSuccessStatusCode();
        var endTurnResult = await response.Content.ReadFromJsonAsync<CardRc>();
        Assert.Equal(CardRc.Ok, endTurnResult);

        response = await clients[1].PostAsJsonAsync(
            $"/api/v1/games/{game.Id}/playCard",
            new PlayAction(_generalStoreId, null, null));
        response.EnsureSuccessStatusCode();
        Assert.Equal(CardRc.WaitConditions, await response.Content.ReadFromJsonAsync<CardRc>());

        for (var i = 1; i < PlayerCount; ++i)
        {
            response = await clients[i].PostAsJsonAsync(
                $"/api/v1/games/{game.Id}/playerAction",
                new WaitAction(_cards[i], null));
            response.EnsureSuccessStatusCode();
            Assert.Equal(CardRc.Ok, await response.Content.ReadFromJsonAsync<CardRc>());
        }
        response = await clients[0].PostAsJsonAsync(
            $"/api/v1/games/{game.Id}/playerAction",
            new WaitAction(_cards[0], null));
        response.EnsureSuccessStatusCode();
        Assert.Equal(CardRc.Ok, await response.Content.ReadFromJsonAsync<CardRc>());

        response = await clients[1].PostAsJsonAsync(
            $"/api/v1/games/{game.Id}/playCard",
            new PlayAction(_bangId, _users[0].Id, null));
        response.EnsureSuccessStatusCode();
        Assert.Equal(CardRc.WaitConditions, await response.Content.ReadFromJsonAsync<CardRc>());

        var playerAction = new WaitAction(null, false);
        response = await clients[0].PostAsJsonAsync(
            $"/api/v1/games/{game.Id}/playerAction", playerAction);
        response.EnsureSuccessStatusCode();
        Assert.Equal(CardRc.OutlawWin, await response.Content.ReadFromJsonAsync<CardRc>());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        db.ChangeTracker.Clear();
        var endGame = await db.Games.FirstAsync(x => x.Id == game.Id);
        Assert.True(endGame.IsEnded);

        var gameState = JsonConvert.DeserializeObject<GameStateDto>(
            StringCompressor.DecompressString(endGame.GameState!)
            )!;
        Assert.Equal(2, gameState.Players[0].CardsInHand.Count);
        Assert.Equal(3, gameState.Players[1].CardsInHand.Count);
        Assert.Single(gameState.Players[2].CardsInHand);
        Assert.Single(gameState.Players[3].CardsInHand);
    }

    [Theory]
    [InlineData("mock")]
    [InlineData("real")]
    public async Task E2E_EmailTest(string receiver)
    {
        var (overrides, emailReceiver) = _emailHandle[receiver];
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(overrides);
            });
        });
        var client = factory.CreateClient();
        var registerRequest = new RegisterOrLoginRequest(
            "user", "bang-no-reply@yandex.ru", "user", true
        );
        var response = await client.PostAsJsonAsync("/api/v1/user/register", registerRequest);
        response.EnsureSuccessStatusCode();
        await Task.Delay(1000);

        var code = await emailReceiver.LastEmailBody();
        var request = new Confirm2FaRequest(registerRequest.Username, code);
        response = await client.PostAsJsonAsync("/api/v1/user/confirmCode", request);
        response.EnsureSuccessStatusCode();
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.NotEmpty(authResponse.Token);
    }
}
