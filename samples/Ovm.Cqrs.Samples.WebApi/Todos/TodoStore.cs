using System.Collections.Concurrent;

namespace Ovm.Cqrs.Samples.WebApi.Todos;

/// <summary>In-memory storage, standing in for a database.</summary>
internal sealed class TodoStore
{
    public ConcurrentDictionary<Guid, Todo> Items { get; } = new();
}

internal sealed record Todo(Guid Id, string Title, bool IsCompleted);
