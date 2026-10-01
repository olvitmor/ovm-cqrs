using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs;

internal sealed class CommandProcessor(IServiceProvider serviceProvider) : ICommandProcessor
{
    public Task<TResult> ProcessCommandAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
    {
        var handler = serviceProvider.GetService<ICommandHandler<TCommand, TResult>>()
                      ?? throw new HandlerNotFoundException(typeof(TCommand));
        var steps = serviceProvider.GetServices<ICommandPipelineStep<TCommand, TResult>>().ToArray();

        CommandPipelineNext<TCommand, TResult> next = handler.HandleAsync;
        for (var i = steps.Length - 1; i >= 0; i--)
        {
            var step = steps[i];
            var inner = next;
            next = (c, ct) => step.HandleAsync(c, inner, ct);
        }

        return next(command, cancellationToken);
    }

    [RequiresDynamicCode(Dispatch.RequiresDynamicCodeMessage)]
    public Task<TResult> ProcessCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        Dispatch.CommandAsync(this, command, cancellationToken);
}
