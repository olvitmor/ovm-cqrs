namespace Ovm.Cqrs.Samples.WebApi.Todos;

internal sealed record ListTodos : IQuery<IReadOnlyList<Todo>>;

internal sealed class ListTodosHandler(TodoStore store) : IQueryHandler<ListTodos, IReadOnlyList<Todo>>
{
    public Task<IReadOnlyList<Todo>> HandleAsync(ListTodos query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Todo>>([.. store.Items.Values.OrderBy(t => t.Title, StringComparer.Ordinal)]);
}
