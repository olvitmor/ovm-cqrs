namespace Ovm.Cqrs.Samples.WebApi.Todos;

internal sealed record GetTodo(Guid Id) : IQuery<Todo?>;

internal sealed class GetTodoHandler(TodoStore store) : IQueryHandler<GetTodo, Todo?>
{
    public Task<Todo?> HandleAsync(GetTodo query, CancellationToken cancellationToken) =>
        Task.FromResult(store.Items.GetValueOrDefault(query.Id));
}
