using System.ComponentModel.DataAnnotations;
using Ovm.Cqrs;
using Ovm.Cqrs.Samples.WebApi.Pipeline;
using Ovm.Cqrs.Samples.WebApi.Todos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TodoStore>();
builder.Services.AddCqrs(cqrs => cqrs
    .AddHandlersFromAssemblyContaining<Program>()
    // Steps run in registration order, first = outermost: logging also sees validation failures.
    .AddCommandPipelineStep(typeof(LoggingCommandStep<,>))
    .AddCommandPipelineStep(typeof(ValidationCommandStep<,>))
    .AddQueryPipelineStep(typeof(LoggingQueryStep<,>)));

var app = builder.Build();

// Turn validation failures from ValidationCommandStep into 400 responses.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ValidationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

var todos = app.MapGroup("/todos");

// Inferred overload: the result type comes from CreateTodo : ICommand<CreateTodoResult>.
todos.MapPost("/", async (CreateTodo command, ICommandProcessor commands, CancellationToken ct) =>
{
    var result = await commands.ProcessCommandAsync(command, ct);
    return Results.Created($"/todos/{result.Id}", result);
});

// Explicit overload: no runtime reflection, safe for Native AOT.
todos.MapPost("/{id:guid}/complete", async (Guid id, ICommandProcessor commands, CancellationToken ct) =>
{
    var result = await commands.ProcessCommandAsync<CompleteTodo, CompleteTodoResult>(new CompleteTodo(id), ct);
    return result.Found ? Results.NoContent() : Results.NotFound();
});

todos.MapGet("/{id:guid}", async (Guid id, IQueryProcessor queries, CancellationToken ct) =>
    await queries.ProcessQueryAsync(new GetTodo(id), ct) is { } todo ? Results.Ok(todo) : Results.NotFound());

todos.MapGet("/", (IQueryProcessor queries, CancellationToken ct) => queries.ProcessQueryAsync(new ListTodos(), ct));

app.Run();
