using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Ovm.Cqrs;

/// <summary>
/// Runtime dispatch for the inferred overloads: closes a generic dispatcher over the message's runtime type
/// once per type, then reuses it.
/// </summary>
internal static class Dispatch
{
    internal const string RequiresDynamicCodeMessage =
        "Resolves the message type at runtime. For Native AOT, use the overload with explicit type arguments.";

    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    public static Task<TResult> CommandAsync<TResult>(ICommandProcessor processor, ICommand<TResult> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var dispatcher = CommandCache<TResult>.Dispatchers.GetOrAdd(command.GetType(), CreateCommandDispatcher<TResult>);
        return dispatcher.DispatchAsync(processor, command, cancellationToken);
    }

    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    public static Task<TResult> QueryAsync<TResult>(IQueryProcessor processor, IQuery<TResult> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var dispatcher = QueryCache<TResult>.Dispatchers.GetOrAdd(query.GetType(), CreateQueryDispatcher<TResult>);
        return dispatcher.DispatchAsync(processor, query, cancellationToken);
    }

    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CommandDispatcher<,>))]
    private static CommandDispatcher<TResult> CreateCommandDispatcher<TResult>(Type commandType) =>
        (CommandDispatcher<TResult>)Activator.CreateInstance(typeof(CommandDispatcher<,>).MakeGenericType(commandType, typeof(TResult)))!;

    [RequiresDynamicCode(RequiresDynamicCodeMessage)]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(QueryDispatcher<,>))]
    private static QueryDispatcher<TResult> CreateQueryDispatcher<TResult>(Type queryType) =>
        (QueryDispatcher<TResult>)Activator.CreateInstance(typeof(QueryDispatcher<,>).MakeGenericType(queryType, typeof(TResult)))!;

    private static class CommandCache<TResult>
    {
        public static readonly ConcurrentDictionary<Type, CommandDispatcher<TResult>> Dispatchers = new();
    }

    private abstract class CommandDispatcher<TResult>
    {
        public abstract Task<TResult> DispatchAsync(ICommandProcessor processor, ICommand<TResult> command, CancellationToken cancellationToken);
    }

    private sealed class CommandDispatcher<TCommand, TResult> : CommandDispatcher<TResult>
        where TCommand : ICommand<TResult>
    {
        public override Task<TResult> DispatchAsync(ICommandProcessor processor, ICommand<TResult> command, CancellationToken cancellationToken) =>
            processor.ProcessCommandAsync<TCommand, TResult>((TCommand)command, cancellationToken);
    }

    private static class QueryCache<TResult>
    {
        public static readonly ConcurrentDictionary<Type, QueryDispatcher<TResult>> Dispatchers = new();
    }

    private abstract class QueryDispatcher<TResult>
    {
        public abstract Task<TResult> DispatchAsync(IQueryProcessor processor, IQuery<TResult> query, CancellationToken cancellationToken);
    }

    private sealed class QueryDispatcher<TQuery, TResult> : QueryDispatcher<TResult>
        where TQuery : IQuery<TResult>
    {
        public override Task<TResult> DispatchAsync(IQueryProcessor processor, IQuery<TResult> query, CancellationToken cancellationToken) =>
            processor.ProcessQueryAsync<TQuery, TResult>((TQuery)query, cancellationToken);
    }
}
