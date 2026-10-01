# Ovm.Cqrs: design decisions

This file records the decisions behind v0.1. Change it when a decision changes.

## Scope

- A lightweight, general-purpose CQRS dispatcher. **Commands and queries only** in v0.1.
  Notifications/events and streaming queries are deferred (tracked as issues).
- Targets **`net10.0` only**.
- License: MIT.

## Package

A single package, **`Ovm.Cqrs`**, depending only on `Microsoft.Extensions.DependencyInjection.Abstractions`
(lowest compatible version, 10.0.0). Logging and EF Core transactions were planned as add-on packages and
dropped: users write them as their own pipeline steps, and the README shows examples. If add-on packages
come back, they share the core's version number (lockstep).

The namespace is flat: everything public is in `Ovm.Cqrs`.
Inside a package, folders organise the source (`Commands/Abstracts`, `Queries/Abstracts`, `Exceptions`)
without changing the namespace. `dotnet_style_namespace_match_folder` is turned off for this reason.

## Core API

### Messages and results

- `ICommand<TResult>`, `IQuery<TResult>`. **`TResult` is unconstrained**: any type can be a result.
- Every command has a result type; there is no void command and no `Unit`. A command with nothing to
  return uses a result type the user defines (e.g. `public sealed record EmptyCommandResult;`).
- `IHasIsSuccessFlag { bool IsSuccess { get; } }` is **optional**. Users' own pipeline steps can read it (e.g. to roll back a transaction).

### Handlers

- `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>`.
- `HandleAsync(message, CancellationToken)`. The `CancellationToken` is **required**.
- The core ships no base handler classes.

### Processors

There are two separate processors, so a read-only consumer can inject only `IQueryProcessor`.

```csharp
// explicit: no reflection, AOT-safe
commands.ProcessCommandAsync<CreateUser, CreateUserResult>(cmd, ct);
// inferred: runtime dispatch, cached per message type
commands.ProcessCommandAsync(cmd, ct);
queries.ProcessQueryAsync(query, ct);
```

- Every method returns `Task<TResult>`. `CancellationToken cancellationToken = default` on processors.
- The inferred overloads are marked `[RequiresDynamicCode]` (they use `MakeGenericType`). Native AOT apps
  get a warning there and should use the explicit overloads.
- Exceptions propagate unwrapped. The core's only own exceptions are `HandlerNotFoundException` and
  `DuplicateHandlerException`; both derive from `InvalidOperationException`.

### Pipeline

- `ICommandPipelineStep<TCommand, TResult>` and `IQueryPipelineStep<TQuery, TResult>`, each receiving a
  `next(message, ct)` delegate of type `CommandPipelineNext<,>` / `QueryPipelineNext<,>`.
- A step is either an **open generic** (it runs for every command or query) or a **closed type** (it runs
  only for the messages it implements the step interface for).
- Steps are registered as Scoped.
- Steps run in **registration order, outermost first**.
- The core ships **no** steps. Cross-cutting concerns such as logging, transactions, validation and authorization are
  steps users write themselves (`ICommandPipelineStep` / `IQueryPipelineStep`); the README shows
  examples. There is no logging package.

### Registration

```csharp
services.AddCqrs(cqrs => cqrs
    .AddHandlersFromAssembly(typeof(Program).Assembly)   // or AddHandlersFromAssemblyContaining<T>()
    .AddCommandPipelineStep(typeof(TransactionStep<,>))
    .AddCommandPipelineStep(typeof(AuthorizationStep<,>))
    .AddQueryPipelineStep(typeof(CachingStep<,>)));
```

- `AddHandler<THandler>(lifetime)` registers one handler explicitly. It needs no scanning, so it is
  trimming/AOT-safe.
- `AddHandlersFromAssembly(asm)` / `AddHandlersFromAssemblyContaining<T>()` register every concrete,
  non-generic handler in an assembly. They are marked `[RequiresUnreferencedCode]`.
- Handlers are **Scoped** by default; the lifetime can be overridden on each `AddHandler…` call.
- **Any** duplicate handler registration throws `DuplicateHandlerException` immediately: two different
  handlers for one message, the same handler registered twice, a duplicate coming from a second `AddCqrs`
  call, or a handler that was already registered manually in the `IServiceCollection`.
- Calling `AddCqrs` more than once is allowed; the processors are registered only once (`TryAdd`).
- A missing handler throws `HandlerNotFoundException` at dispatch, with a hint about registration.
- There is no startup check that every message has a handler in v0.1.

## Engineering

- Layout: `src/`, `tests/`, `samples/`, `Ovm.Cqrs.slnx`, `Directory.Build.props`,
  `Directory.Packages.props` (Central Package Management), `global.json`.
- Public APIs require XML docs, written in English. Warnings are errors.
- Builds are deterministic, with SourceLink, `.snupkg` symbol packages and package validation.
- Tests: xUnit v3 with the built-in `Assert`, on Microsoft.Testing.Platform. Built test-first.

## Versioning and release

- MinVer reads the version from `v*` tags. The first release is `v0.1.0`; while on 0.x, breaking
  changes are allowed.
- GitHub Actions runs CI on PRs and pushes to `main`. A `v*` tag publishes to nuget.org (Trusted
  Publishing) and creates a GitHub Release.
- Azure Pipelines publishes each `main` build as a prerelease to the private Azure Artifacts feed `ovm`.
- See [ci-cd-guide.md](ci-cd-guide.md).
- After the first release, request the `Ovm.` ID prefix reservation on nuget.org.

## Documentation

- The root README is the full documentation. Each package has a short README for its nuget.org page.
- Release notes are generated by GitHub; there is no `CHANGELOG.md`.
