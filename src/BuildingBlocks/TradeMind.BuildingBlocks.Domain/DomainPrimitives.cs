namespace TradeMind.BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}

public abstract class Entity<TId> where TId : notnull
{
    protected Entity(TId id) => Id = id;

    public TId Id { get; }
}

public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

public readonly record struct Result(bool IsSuccess, Error Error)
{
    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}
