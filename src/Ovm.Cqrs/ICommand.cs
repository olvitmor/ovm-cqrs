namespace Ovm.Cqrs;

/// <summary>
/// A command: a request that changes state and returns <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type of the command result.</typeparam>
public interface ICommand<TResult>;

/// <summary>
/// A command that changes state and returns nothing (its result is <see cref="Unit"/>).
/// </summary>
public interface ICommand : ICommand<Unit>;
