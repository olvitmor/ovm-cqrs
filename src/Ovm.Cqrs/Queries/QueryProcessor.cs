using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs;

internal sealed class QueryProcessor(IServiceProvider serviceProvider) : IQueryProcessor
{
    public Task<TResult> ProcessQueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
    {
        var handler = serviceProvider.GetService<IQueryHandler<TQuery, TResult>>()
                      ?? throw new HandlerNotFoundException(typeof(TQuery));
        var steps = serviceProvider.GetServices<IQueryPipelineStep<TQuery, TResult>>().ToArray();

        QueryPipelineNext<TQuery, TResult> next = handler.HandleAsync;
        for (var i = steps.Length - 1; i >= 0; i--)
        {
            var step = steps[i];
            var inner = next;
            next = (q, ct) => step.HandleAsync(q, inner, ct);
        }

        return next(query, cancellationToken);
    }

    [RequiresDynamicCode(Dispatch.RequiresDynamicCodeMessage)]
    public Task<TResult> ProcessQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        Dispatch.QueryAsync(this, query, cancellationToken);
}
