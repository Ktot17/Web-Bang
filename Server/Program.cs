using System.Text;
using BLComponent;
using BLComponent.InputPorts;
using BLComponent.OutputPort;
using DBComponent;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Server.InputPorts;
using Server.Utils;
using ILogger = Serilog.ILogger;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BLIntegrationTests")]

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHostedService<GameCleanup>();

var dbType = (Db)builder.Configuration.GetValue<int>("Db");

switch (dbType)
{
    case Db.Postgres:
        builder.Services.AddDbContext<DBComponent.Postgres.ServerDbContext>(options =>
            options.UseNpgsql(builder.Configuration["ConnectionStrings:Postgres"]!));
        builder.Services.AddScoped<IUserRepository, DBComponent.Postgres.UserRepository>();
        builder.Services.AddScoped<IGameRepository, DBComponent.Postgres.GameRepository>();
        builder.Services.AddScoped<ICardRepository, DBComponent.Postgres.CardRepository>();
        break;
    case Db.MySql:
        builder.Services.AddDbContext<DBComponent.MySql.ServerDbContext>(options =>
            options.UseMySql(builder.Configuration["ConnectionStrings:MySql"]!,
                ServerVersion.AutoDetect(builder.Configuration["ConnectionStrings:MySql"]!)));
        builder.Services.AddScoped<IUserRepository, DBComponent.MySql.UserRepository>();
        builder.Services.AddScoped<IGameRepository, DBComponent.MySql.GameRepository>();
        builder.Services.AddScoped<ICardRepository, DBComponent.MySql.CardRepository>();
        break;
    case Db.MongoDb:
        var mongoConnection = builder.Configuration["ConnectionStrings:MongoDb"]!;
        var mongoDatabase = builder.Configuration["MongoDb:Database"]!;

        builder.Services.AddSingleton(new DBComponent.MongoDb.ServerDbContext(mongoConnection, mongoDatabase));
        builder.Services.AddScoped<IUserRepository, DBComponent.MongoDb.UserRepository>();
        builder.Services.AddScoped<IGameRepository, DBComponent.MongoDb.GameRepository>();
        builder.Services.AddScoped<ICardRepository, DBComponent.MongoDb.CardRepository>();
        break;
    default:
        throw new ArgumentException("Unknown Db type");
}

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddScoped<IGameManager, GameManager>();
builder.Services.AddScoped<ITokenGenerator, TokenGenerator>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<ILogger>(_ => logger);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    switch (dbType)
    {
        case Db.Postgres:
            {
                var db = scope.ServiceProvider.GetRequiredService<DBComponent.Postgres.ServerDbContext>();
                await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
                break;
            }
        case Db.MySql:
            {
                var db = scope.ServiceProvider.GetRequiredService<DBComponent.MySql.ServerDbContext>();
                await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
                break;
            }
        case Db.MongoDb:
            break;
        default:
            throw new ArgumentException("Unknown Db type");
    }
    var log = scope.ServiceProvider.GetRequiredService<ILogger>();
    log.Information("Starting up");
}

var isReadOnly = builder.Configuration.GetValue<bool>("ReadOnly");

if (isReadOnly)
{
    var writeMethods = new[] { "POST", "PUT", "PATCH", "DELETE" };

    app.Use(async (context, next) =>
    {
        if (writeMethods.Contains(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("This server is read-only").ConfigureAwait(false);
            return;
        }

        await next().ConfigureAwait(false);
    });
}

app.UseSwagger(options =>
{
    options.RouteTemplate = "api/v1/swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/api/v1/swagger/v1/swagger.json", "API V1");
    options.RoutePrefix = "api/v1/swagger";
});

app.UseHttpsRedirection();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<GameHub>("/api/v1/hub");
app.MapControllers();
app.MapGet("api/v1/health", () => Results.Ok("Healthy"));

await app.RunAsync().ConfigureAwait(false);

public abstract partial class Program;
