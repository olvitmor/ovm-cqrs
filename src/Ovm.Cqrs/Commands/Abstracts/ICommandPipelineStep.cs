namespace Ovm.Cqrs;

/// <summary>
/// Invokes the next step of the command pipeline (another step, or finally the handler).
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The type of the command result.</typeparam>
/// <param name="command">The command to pass on.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>The command result.</returns>
public delegate Task<TResult> CommandPipelineNext<in TCommand, TResult>(TCommand command, CancellationToken cancellationToken)
    where TCommand : ICommand<TResult>;

/// <summary>
/// A step that wraps command handling, e.g. for transactions or authorization.
/// Register it with <see cref="CqrsBuilder.AddCommandPipelineStep"/>.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The type of the command result.</typeparam>
public interface ICommandPipelineStep<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Handles the command, calling <paramref name="next"/> to continue the pipeline or returning early to short-circuit it.
    /// </summary>
    /// <param name="command">The command being dispatched.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The command result.</returns>
    Task<TResult> HandleAsync(TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken);
}
