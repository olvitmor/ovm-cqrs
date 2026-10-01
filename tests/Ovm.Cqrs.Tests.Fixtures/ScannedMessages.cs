namespace Ovm.Cqrs.Tests.Fixtures;

public sealed record ScannedQuery(string Name) : IQuery<string>;

public sealed record ScannedCommand(int Value) : ICommand<int>;

/// <summary>A user-style abstract base handler: scanning must skip it and register only the concrete subclass.</summary>
public abstract class BaseQueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public abstract Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public sealed class ScannedQueryHandler : BaseQueryHandler<ScannedQuery, string>
{
    public override Task<string> HandleAsync(ScannedQuery query, CancellationToken cancellationToken) =>
        Task.FromResult($"scanned {query.Name}");
}

public sealed class ScannedCommandHandler : ICommandHandler<ScannedCommand, int>
{
    public Task<int> HandleAsync(ScannedCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(command.Value * 2);
}
