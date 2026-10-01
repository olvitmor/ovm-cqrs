# Ovm.Cqrs

A small CQRS dispatcher for .NET 10. You get commands, queries, separate processors for each, and
pipeline steps around every handler. Its only dependency is
`Microsoft.Extensions.DependencyInjection.Abstractions`.

```csharp
public sealed record CreateTodo(string Title) : ICommand<CreateTodoResult>;
public sealed record CreateTodoResult(Guid Id);

public sealed class CreateTodoHandler(TodoStore store) : ICommandHandler<CreateTodo, CreateTodoResult>
{
    public Task<CreateTodoResult> HandleAsync(CreateTodo command, CancellationToken cancellationToken) =>
        Task.FromResult(new CreateTodoResult(store.Add(command.Title)));
}

// Registration
services.AddCqrs(cqrs => cqrs
    .AddHandlersFromAssemblyContaining<Program>()
    .AddCommandPipelineStep(typeof(LoggingCommandStep<,>)));

// Dispatch
var result = await commands.ProcessCommandAsync(new CreateTodo("Buy milk"), ct);
```

- `ICommandProcessor` and `IQueryProcessor` are separate services.
- Pipeline steps are your own (logging, validation, authorization, transactions…) and run in
  registration order, the first one registered outermost.
- Duplicate handlers fail at registration.
- Supports trimming and Native AOT through `AddHandler<T>()` and the explicit dispatch overloads.

Full documentation, with step examples: https://github.com/olvitmor/ovm-cqrs
