namespace Ovm.Cqrs.Samples.WebApi.Todos;

internal sealed record CompleteTodo(Guid Id) : ICommand<CompleteTodoResult>;

/// <summary>Reports "not found" as data instead of throwing.</summary>
internal sealed record CompleteTodoResult(bool Found);

internal sealed class CompleteTodoHandler(TodoStore store) : ICommandHandler<CompleteTodo, CompleteTodoResult>
{
    public Task<CompleteTodoResult> HandleAsync(CompleteTodo command, CancellationToken cancellationToken)
    {
        if (!store.Items.TryGetValue(command.Id, out var todo))
            return Task.FromResult(new CompleteTodoResult(Found: false));

        store.Items[command.Id] = todo with { IsCompleted = true };
        return Task.FromResult(new CompleteTodoResult(Found: true));
    }
}
