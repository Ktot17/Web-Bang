using BLComponent;
using MongoDB.Driver;
using Server.Models;

namespace DBComponent.MongoDb;

public class ServerDbContext
{
    private readonly IMongoDatabase _database;

    public ServerDbContext(string connectionString, string dbName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(dbName);
    }

    public IMongoCollection<Game> Games => _database.GetCollection<Game>("games");
    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<CardDb> Cards => _database.GetCollection<CardDb>("classicdeck");
}
