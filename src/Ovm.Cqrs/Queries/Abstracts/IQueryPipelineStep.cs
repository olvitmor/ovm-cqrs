namespace Ovm.Cqrs;

/// <summary>
/// Invokes the next step of the query pipeline (another step, or finally the handler).
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The type of the query result.</typeparam>
/// <param name="query">The query to pass on.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>The query result.</returns>
public delegate Task<TResult> QueryPipelineNext<in TQuery, TResult>(TQuery query, CancellationToken cancellationToken)
    where TQuery : IQuery<TResult>;

/// <summary>
/// A step that wraps query handling, e.g. for caching or authorization.
/// Register it with <see cref="CqrsBuilder.AddQueryPipelineStep"/>.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The type of the query result.</typeparam>
public interface IQueryPipelineStep<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the query, calling <paramref name="next"/> to continue the pipeline or returning early to short-circuit it.
    /// </summary>
    /// <param name="query">The query being dispatched.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The query result.</returns>
    Task<TResult> HandleAsync(TQuery query, QueryPipelineNext<TQuery, TResult> next, CancellationToken cancellationToken);
}
