namespace Ovm.Cqrs;

/// <summary>
/// Thrown at dispatch time when no handler is registered for a command or query.
/// </summary>
public sealed class HandlerNotFoundException : InvalidOperationException
{
    /// <summary>
    /// Creates the exception for the given message type.
    /// </summary>
    /// <param name="messageType">The command or query type that has no handler.</param>
    public HandlerNotFoundException(Type messageType)
        : base($"No handler is registered for '{messageType?.FullName}'. " +
               "Register it with AddHandler<THandler>() or AddHandlersFromAssembly(...) inside AddCqrs(...).")
    {
        ArgumentNullException.ThrowIfNull(messageType);
        MessageType = messageType;
    }

    /// <summary>
    /// The command or query type that has no handler.
    /// </summary>
    public Type MessageType { get; }
}
