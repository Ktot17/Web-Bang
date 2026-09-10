namespace DBComponent;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(Type entityType)
        : base(
            $"Entity of type {(entityType ?? throw new ArgumentNullException(nameof(entityType))).Name} was not found.")
    { }

    public EntityNotFoundException() { }

    public EntityNotFoundException(string message) : base(message) { }

    public EntityNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}

public enum Db
{
    Postgres,
    MySql,
    MongoDb,
}
