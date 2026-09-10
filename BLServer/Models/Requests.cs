namespace Server.Models;

public record RegisterOrLoginRequest(string Username, string Email, string Password, bool NeedTwoFactor);

public record Confirm2FaRequest(string Username, string Code);

public record PlayAction(Guid CardId, Guid? TargetPlayerId, Guid? TargetCardId);

public record WaitAction(Guid? TargetPlayerId, bool? YesOrNo);
