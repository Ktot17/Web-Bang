using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DBComponent.Postgres;
using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit2;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Server.Models;
using Server.Utils;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: LightBddScope]

namespace E2ETests;

[FeatureDescription("User tries to authorize with 2FA")]
public class AuthE2ETests : FeatureFixture, IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string Password = "password";
    private readonly User _user;
    private readonly HttpClient _mailClient;

    private HttpResponseMessage? _response;
    private string? _code;
    private string? _loginMessage;

    public AuthE2ETests(WebApplicationFactory<Program> factory)
    {
        var dbName = Guid.NewGuid().ToString();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var overrides = new Dictionary<string, string?>
                {
                    ["SMTP:Host"] = "localhost",
                    ["SMTP:Port"] = "1025",
                    ["SMTP:UseDefaultCredentials"] = "true",
                    ["SMTP:EnableSsl"] = "false"
                };

                config.AddInMemoryCollection(overrides);
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

        _user = new User(Guid.NewGuid(),
            "test", "bang-no-reply@yandex.ru", PasswordHasher.HashPassword(Password), null);

        _mailClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:8025/")
        };
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

    private async Task GivenUserExists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        db.Users.Add(_user);
        await db.SaveChangesAsync();
    }

    private async Task WhenUserLoginsSuccessfully()
    {
        var client = _factory.CreateClient();
        var request = new RegisterOrLoginRequest(_user.Name, _user.Email,
            Password, true);
        _response = await client.PostAsJsonAsync("/api/v1/user/login", request);
        _response.EnsureSuccessStatusCode();
        _loginMessage = _response.Content.ReadAsStringAsync().Result;
    }

    private async Task WhenEmailArrivesSuccessfully()
    {
        var response = await _mailClient.GetStringAsync("api/v2/messages");
        using var messagesJson = JsonDocument.Parse(response);
        _code = messagesJson.RootElement.GetProperty("items")[0]
            .GetProperty("Content").GetProperty("Body").GetString()!.TrimEnd();
        var messageId = messagesJson.RootElement.GetProperty("items")[0]
            .GetProperty("ID").GetString()!;
        await _mailClient.DeleteAsync($"/api/v1/messages/{messageId}");
    }

    private async Task WhenUserConfirmsCodeSuccessfully()
    {
        var client = _factory.CreateClient();
        var request = new Confirm2FaRequest(_user.Name, _code!);
        _response = await client.PostAsJsonAsync("/api/v1/user/confirmCode", request);
        _response.EnsureSuccessStatusCode();
    }

    private async Task ThenThereIsJwtToken()
    {
        Assert.NotNull(_response);
        var response = await _response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(response);
        Assert.NotEmpty(response.Token);
    }

    [Scenario]
    public async Task SuccessfulLoginWith2Fa()
    {
        await Runner.RunScenarioAsync(
            _ => GivenUserExists(),
            _ => WhenUserLoginsSuccessfully(),
            _ => WhenEmailArrivesSuccessfully(),
            _ => WhenUserConfirmsCodeSuccessfully(),
            _ => ThenThereIsJwtToken()
            );
    }

    private async Task GivenUserWithOutDatedPasswordExists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        _user.LastPasswordChange = DateTimeOffset.UtcNow.DateTime - TimeSpan.FromDays(100);
        db.Users.Add(_user);
        await db.SaveChangesAsync();
    }

    private Task ThenThereIsMessageAfterLogin()
    {
        Assert.NotNull(_loginMessage);
        return Task.CompletedTask;
    }

    [Scenario]
    public async Task LoginWhenPasswordIsOutdated()
    {
        await Runner.RunScenarioAsync(
            _ => GivenUserWithOutDatedPasswordExists(),
            _ => WhenUserLoginsSuccessfully(),
            _ => ThenThereIsMessageAfterLogin()
        );
    }

    private async Task WhenUserLoginFailsTooManyTimes()
    {
        for (var i = 0; i < 6; ++i)
        {
            var client = _factory.CreateClient();
            var request = new RegisterOrLoginRequest(_user.Name, _user.Email,
                "wrong password", true);
            _response = await client.PostAsJsonAsync("/api/v1/user/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, _response.StatusCode);
            _loginMessage = _response.Content.ReadAsStringAsync().Result;
        }
    }

    private Task ThenAccountLocked()
    {
        Assert.NotNull(_loginMessage);
        Assert.Contains("Account locked until", _loginMessage);
        return Task.CompletedTask;
    }

    private async Task WhenUserWaitsEndOfLock()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        _user.LockoutEnd = DateTimeOffset.UtcNow.DateTime;
        db.Users.Update(_user);
        await db.SaveChangesAsync();
    }

    [Scenario]
    public async Task TooManyFailedLogins()
    {
        await Runner.RunScenarioAsync(
            _ => GivenUserExists(),
            _ => WhenUserLoginFailsTooManyTimes(),
            _ => ThenAccountLocked(),
            _ => WhenUserWaitsEndOfLock(),
            _ => WhenUserLoginsSuccessfully(),
            _ => WhenEmailArrivesSuccessfully(),
            _ => WhenUserConfirmsCodeSuccessfully(),
            _ => ThenThereIsJwtToken()
        );
    }
}
