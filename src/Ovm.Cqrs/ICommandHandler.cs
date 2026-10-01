namespace Ovm.Cqrs;

/// <summary>
/// Handles a command of type <typeparamref name="TCommand"/>.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The type of the command result.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The command result.</returns>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a command of type <typeparamref name="TCommand"/> that returns nothing.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the command has been handled.</returns>
    new Task HandleAsync(TCommand command, CancellationToken cancellationToken);

    async Task<Unit> ICommandHandler<TCommand, Unit>.HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        await HandleAsync(command, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
