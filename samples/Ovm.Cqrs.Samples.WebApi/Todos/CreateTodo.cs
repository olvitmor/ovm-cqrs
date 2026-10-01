using System.ComponentModel.DataAnnotations;

namespace Ovm.Cqrs.Samples.WebApi.Todos;

internal sealed record CreateTodo([property: Required, StringLength(100, MinimumLength = 1)] string Title)
    : ICommand<CreateTodoResult>;

internal sealed record CreateTodoResult(Guid Id);

internal sealed class CreateTodoHandler(TodoStore store) : ICommandHandler<CreateTodo, CreateTodoResult>
{
    public Task<CreateTodoResult> HandleAsync(CreateTodo command, CancellationToken cancellationToken)
    {
        var todo = new Todo(Guid.NewGuid(), command.Title, IsCompleted: false);
        store.Items[todo.Id] = todo;
        return Task.FromResult(new CreateTodoResult(todo.Id));
    }
}
