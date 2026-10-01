using System.Diagnostics.CodeAnalysis;

namespace Ovm.Cqrs;

/// <summary>
/// Entry point for dispatching commands to their handlers through the command pipeline.
/// </summary>
public interface ICommandProcessor
{
    /// <summary>
    /// Dispatches a command to its handler. The explicit type arguments avoid any runtime reflection.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResult">The type of the command result.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The command result.</returns>
    Task<TResult> ProcessCommandAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>;

    /// <summary>
    /// Dispatches a command to its handler, inferring the result type from the command.
    /// The command type is resolved at runtime once per type and cached.
    /// </summary>
    /// <typeparam name="TResult">The type of the command result.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The command result.</returns>
    [RequiresDynamicCode(Dispatch.RequiresDynamicCodeMessage)]
    Task<TResult> ProcessCommandAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);
}
