namespace Ovm.Cqrs;

/// <summary>
/// A query: a request that reads state and returns <typeparamref name="TResult"/> without changing anything.
/// </summary>
/// <typeparam name="TResult">The type of the query result.</typeparam>
public interface IQuery<TResult>;
