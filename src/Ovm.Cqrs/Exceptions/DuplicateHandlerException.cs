namespace Ovm.Cqrs;

/// <summary>
/// Thrown during <see cref="CqrsServiceCollectionExtensions.AddCqrs"/> when two different handlers are
/// registered for the same command or query.
/// </summary>
public sealed class DuplicateHandlerException : InvalidOperationException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="messageType">The command or query type with more than one handler.</param>
    /// <param name="existingHandlerType">The handler registered first.</param>
    /// <param name="duplicateHandlerType">The handler registered second.</param>
    public DuplicateHandlerException(Type messageType, Type existingHandlerType, Type duplicateHandlerType)
        : base($"'{messageType?.FullName}' already has a handler ('{existingHandlerType?.FullName}'); " +
               $"cannot also register '{duplicateHandlerType?.FullName}'. Each command and query must have exactly one handler.")
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(existingHandlerType);
        ArgumentNullException.ThrowIfNull(duplicateHandlerType);
        MessageType = messageType;
        HandlerTypes = [existingHandlerType, duplicateHandlerType];
    }

    /// <summary>
    /// The command or query type with more than one handler.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// The conflicting handler types, in registration order.
    /// </summary>
    public IReadOnlyList<Type> HandlerTypes { get; }
}
