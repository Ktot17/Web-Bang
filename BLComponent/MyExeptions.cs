namespace BLComponent;

public class WrongNumberOfPlayersException : Exception
{
    public WrongNumberOfPlayersException(int players)
        : base($"Wrong number of players. Expected between {GameManager.MinPlayersCount} and "
               + $"{GameManager.MaxPlayersCount}, but got {players}.")
    { }

    public WrongNumberOfPlayersException() { }

    public WrongNumberOfPlayersException(string message) : base(message) { }

    public WrongNumberOfPlayersException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class NotUniqueNamesException : Exception
{
    public NotUniqueNamesException() : base("Not unique names.") { }

    public NotUniqueNamesException(string message) : base(message) { }

    public NotUniqueNamesException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class NotExistingGuidException : Exception
{
    public NotExistingGuidException() : base("There are no game objects with this Guid.") { }

    public NotExistingGuidException(string message) : base(message) { }

    public NotExistingGuidException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class NotExistingRoleException : Exception
{
    public NotExistingRoleException() : base("There are no such role.") { }

    public NotExistingRoleException(string message) : base(message) { }

    public NotExistingRoleException(string message, Exception innerException)
        : base(message, innerException) { }
}
