using System.ComponentModel.DataAnnotations.Schema;
using System.IO.Compression;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models;

[Table("games")]
public class Game(Guid id, Guid hostId, string? gameState, bool isEnded, int playerCount)
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    [Column("id")]
    public Guid Id { get; init; } = id;
    [BsonRepresentation(BsonType.String)]
    [Column("host_id")]
    public Guid HostId { get; init; } = hostId;
    [Column("game_state")]
    public string? GameState { get; set; } = gameState;
    [Column("is_ended")]
    public bool IsEnded { get; set; } = isEnded;
    [Column("player_count")]
    public int PlayerCount { get; set; } = playerCount;
    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTimeOffset.UtcNow.DateTime;
}

public static class StringCompressor
{
    public static string CompressString(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string DecompressString(string data)
    {
        var bytes = Convert.FromBase64String(data);
        using var ms = new MemoryStream(bytes);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
