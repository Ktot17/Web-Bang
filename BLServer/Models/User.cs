using System.ComponentModel.DataAnnotations.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models;

[Table("users")]
public class User(Guid id, string name, string email, string passwordHash, Guid? gameId)
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    [Column("id")]
    public Guid Id { get; init; } = id;
    [Column("name")]
    public string Name { get; set; } = name;
    [Column("email")]
    public string Email { get; set; } = email;
    [Column("two_factor_code")]
    public string? Code { get; set; }
    [Column("two_factor_expire")]
    public DateTime? TwoFactorExpire { get; set; }
    [Column("password_hash")]
    public string PasswordHash { get; set; } = passwordHash;
    [BsonRepresentation(BsonType.String)]
    [Column("game_id")]
    public Guid? GameId { get; set; } = gameId;
    [Column("failed_login_count")]
    public int FailedLoginCount { get; set; }
    [Column("lockout_end")]
    public DateTime? LockoutEnd { get; set; }
    [Column("last_password_change")]
    public DateTime LastPasswordChange { get; set; } = DateTimeOffset.UtcNow.DateTime;
}
