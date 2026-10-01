# Ovm.Cqrs

[![NuGet](https://img.shields.io/nuget/v/Ovm.Cqrs.svg)](https://www.nuget.org/packages/Ovm.Cqrs)
[![CI](https://github.com/olvitmor/ovm-cqrs/actions/workflows/ci.yml/badge.svg)](https://github.com/olvitmor/ovm-cqrs/actions/workflows/ci.yml)

A small CQRS dispatcher for .NET 10. You get commands, queries, separate processors for each, and
pipeline steps around every handler.

- **Commands and queries are kept apart.** `ICommandProcessor` and `IQueryProcessor` are separate
  services, so a read-only component can inject only `IQueryProcessor`.
- **Pipeline steps** wrap handlers for logging, validation, authorization, transactions or caching.
  You write them; the library ships none.
- **One dependency:** `Microsoft.Extensions.DependencyInjection.Abstractions`.
- **Supports trimming and Native AOT** through explicit registration and explicit dispatch.
- MIT licensed.

## Install

```bash
dotnet add package Ovm.Cqrs
```

## Quickstart

Define a command, a query and their handlers:

```csharp
using Ovm.Cqrs;

public sealed record CreateTodo(string Title) : ICommand<CreateTodoResult>;
public sealed record CreateTodoResult(Guid Id);

public sealed class CreateTodoHandler(TodoStore store) : ICommandHandler<CreateTodo, CreateTodoResult>
{
    public Task<CreateTodoResult> HandleAsync(CreateTodo command, CancellationToken cancellationToken)
    {
        var id = store.Add(command.Title);
        return Task.FromResult(new CreateTodoResult(id));
    }
}

public sealed record GetTodo(Guid Id) : IQuery<Todo?>;

public sealed class GetTodoHandler(TodoStore store) : IQueryHandler<GetTodo, Todo?>
{
    public Task<Todo?> HandleAsync(GetTodo query, CancellationToken cancellationToken) =>
        Task.FromResult(store.Find(query.Id));
}
```

Register the handlers:

```csharp
builder.Services.AddCqrs(cqrs => cqrs.AddHandlersFromAssemblyContaining<Program>());
```

Dispatch them:

```csharp
app.MapPost("/todos", async (CreateTodo command, ICommandProcessor commands, CancellationToken ct) =>
{
    var result = await commands.ProcessCommandAsync(command, ct);   // result is CreateTodoResult
    return Results.Created($"/todos/{result.Id}", result);
});

app.MapGet("/todos/{id:guid}", async (Guid id, IQueryProcessor queries, CancellationToken ct) =>
    await queries.ProcessQueryAsync(new GetTodo(id), ct) is { } todo ? Results.Ok(todo) : Results.NotFound());
```

[`samples/Ovm.Cqrs.Samples.WebApi`](samples/Ovm.Cqrs.Samples.WebApi) contains a complete, runnable version
of this example, with logging and validation steps.

## Commands, queries and results

- A command (`ICommand<TResult>`) changes state. A query (`IQuery<TResult>`) reads state.
- `TResult` can be **any type**: a record, a DTO, `int`, a nullable type, or your own `Result<T>` type.
- Every command declares a result type. A command with nothing to return can use an empty record you define:

  ```csharp
  public sealed record EmptyCommandResult;
  public sealed record DeleteTodo(Guid Id) : ICommand<EmptyCommandResult>;
  ```

- Each command and each query has **exactly one** handler.

## Two ways to dispatch

```csharp
// 1. Inferred: the result type comes from the command's ICommand<TResult>.
var result = await commands.ProcessCommandAsync(new CreateTodo("Buy milk"), ct);

// 2. Explicit: no runtime reflection.
var result = await commands.ProcessCommandAsync<CreateTodo, CreateTodoResult>(new CreateTodo("Buy milk"), ct);
```

| | Inferred | Explicit |
|---|---|---|
| Call site | short | spells out both types |
| How it works | resolves the message type at runtime, once per type, then caches it | fully static |
| Native AOT | not supported (`[RequiresDynamicCode]` warning) | supported |

`IQueryProcessor.ProcessQueryAsync` has the same two overloads.

## Registration

```csharp
services.AddCqrs(cqrs => cqrs
    // Handlers: scan an assembly…
    .AddHandlersFromAssemblyContaining<Program>()
    // …or register them one by one (trimming/AOT-safe):
    .AddHandler<CreateTodoHandler>()
    .AddHandler<GetTodoHandler>(ServiceLifetime.Transient)

    // Pipeline steps
    .AddCommandPipelineStep(typeof(LoggingCommandStep<,>))
    .AddQueryPipelineStep(typeof(LoggingQueryStep<,>)));
```

- Handlers are **scoped** by default. Each `AddHandler…` call can set a different lifetime.
- **Duplicate handlers fail fast.** `AddCqrs` throws `DuplicateHandlerException` as soon as a second
  handler is registered for the same message: a different handler, the same one twice, one from a
  second `AddCqrs` call, or one you registered manually.
- **A missing handler** throws `HandlerNotFoundException` when the message is dispatched.
- Assembly scanning picks up every concrete, non-generic class that implements `ICommandHandler<,>` or
  `IQueryHandler<,>`, and skips abstract base handlers.

## Pipeline steps

A step wraps the handler. It can run code before and after the handler, change the result, or
**short-circuit** the pipeline by returning without calling `next`.

```csharp
public sealed class MyCommandStep<TCommand, TResult> : ICommandPipelineStep<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(
        TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken)
    {
        // before
        var result = await next(command, cancellationToken);
        // after
        return result;
    }
}
```

- Command steps (`ICommandPipelineStep<,>`) run only for commands, and query steps
  (`IQueryPipelineStep<,>`) run only for queries.
- An **open generic** step (`typeof(MyStep<,>)`) runs for every command or query. A **closed** step,
  for example `class AuditDeleteTodo : ICommandPipelineStep<DeleteTodo, EmptyCommandResult>`, runs only
  for the messages it implements the step interface for.
- **Order:** steps run in registration order, and the first one registered is the **outermost**.

```csharp
.AddCommandPipelineStep(typeof(LoggingStep<,>))      // 1st: outermost, sees everything below
.AddCommandPipelineStep(typeof(ValidationStep<,>))   // 2nd
.AddCommandPipelineStep(typeof(TransactionStep<,>))  // 3rd: innermost, closest to the handler
```

Steps are resolved from the same DI scope as the handler, so they can inject anything the handler can.

### Example: logging

```csharp
public sealed class LoggingCommandStep<TCommand, TResult>(ILogger<LoggingCommandStep<TCommand, TResult>> logger)
    : ICommandPipelineStep<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(
        TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next(command, cancellationToken);
            logger.LogInformation("Handled {Command} in {Elapsed}", typeof(TCommand).Name, Stopwatch.GetElapsedTime(started));
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Command} failed after {Elapsed}", typeof(TCommand).Name, Stopwatch.GetElapsedTime(started));
            throw;
        }
    }
}
```

Be careful about logging the command object itself: commands often carry personal data or secrets.

### Example: authorization

```csharp
public interface IRequirePermission
{
    string Permission { get; }
}

public sealed record DeleteTodo(Guid Id) : ICommand<EmptyCommandResult>, IRequirePermission
{
    public string Permission => "todos:delete";
}

public sealed class AuthorizationStep<TCommand, TResult>(ICurrentUser user) : ICommandPipelineStep<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(
        TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken)
    {
        if (command is IRequirePermission p && !user.HasPermission(p.Permission))
            throw new UnauthorizedAccessException($"Missing permission '{p.Permission}'.");

        return next(command, cancellationToken);
    }
}
```

(`ICurrentUser` stands for your own service.)

### Example: EF Core transaction per command

```csharp
public sealed class TransactionStep<TCommand, TResult>(AppDbContext db) : ICommandPipelineStep<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(
        TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken)
    {
        // A command dispatched from inside another handler joins the outer transaction.
        if (db.Database.CurrentTransaction is not null)
            return await next(command, cancellationToken);

        // Required when the provider uses a retrying strategy, e.g. EnableRetryOnFailure().
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await next(command, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;   // on an exception, disposing the transaction rolls it back
        });
    }
}
```

With a retrying execution strategy, a transient failure **re-runs everything inside `ExecuteAsync`,
including the handler**. Handlers must therefore be safe to run more than once.

## Exceptions

The processors don't wrap or swallow exceptions: whatever a handler or step throws reaches the caller
unchanged. The library throws only two exceptions of its own, both derived from `InvalidOperationException`:

| Exception | When | Properties |
|---|---|---|
| `DuplicateHandlerException` | while calling `AddCqrs` | `MessageType`, `HandlerTypes` |
| `HandlerNotFoundException` | when a message is dispatched | `MessageType` |

## Trimming and Native AOT

The library is annotated for the trimming and AOT analyzers. In a trimmed or Native AOT app:

- register handlers with `AddHandler<THandler>()` instead of assembly scanning, which is marked
  `[RequiresUnreferencedCode]`;
- dispatch with the explicit overloads `ProcessCommandAsync<TCommand, TResult>` and
  `ProcessQueryAsync<TQuery, TResult>`, because the inferred ones are marked `[RequiresDynamicCode]`.

## Compared with a general-purpose mediator

- Commands and queries have **separate processors and separate pipelines**, so a read-only service can
  depend only on `IQueryProcessor`, and query steps (e.g. caching) never touch commands.
- Every message has exactly one handler. There are no notifications or publish/subscribe.
- Nothing is built in: there are no default steps and no result type you're forced to use.

## Building from source

Requires the .NET 10 SDK.

```bash
./build.sh            # restore, build (warnings as errors), test, pack into ./artifacts
```

## License

[MIT](LICENSE)
