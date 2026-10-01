using System.Diagnostics.CodeAnalysis;

namespace Ovm.Cqrs;

/// <summary>
/// Entry point for dispatching queries to their handlers through the query pipeline.
/// </summary>
public interface IQueryProcessor
{
    /// <summary>
    /// Dispatches a query to its handler. The explicit type arguments avoid any runtime reflection.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The type of the query result.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The query result.</returns>
    Task<TResult> ProcessQueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>;

    /// <summary>
    /// Dispatches a query to its handler, inferring the result type from the query.
    /// The query type is resolved at runtime once per type and cached.
    /// </summary>
    /// <typeparam name="TResult">The type of the query result.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The query result.</returns>
    [RequiresDynamicCode(Dispatch.RequiresDynamicCodeMessage)]
    Task<TResult> ProcessQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
