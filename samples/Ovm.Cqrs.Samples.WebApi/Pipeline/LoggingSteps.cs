using System.Diagnostics;

namespace Ovm.Cqrs.Samples.WebApi.Pipeline;

/// <summary>Logs every command's name and duration. Payloads are deliberately not logged: they may contain personal data.</summary>
internal sealed class LoggingCommandStep<TCommand, TResult>(ILogger<LoggingCommandStep<TCommand, TResult>> logger)
    : ICommandPipelineStep<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private static readonly string CommandName = typeof(TCommand).Name;

    public async Task<TResult> HandleAsync(TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next(command, cancellationToken);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Log.Handled(logger, "command", CommandName, elapsedMs);
            return result;
        }
        catch (Exception ex)
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Log.Failed(logger, ex, "command", CommandName, elapsedMs);
            throw;
        }
    }
}

/// <summary>The same for queries. Steps are per side, so commands and queries can be treated differently.</summary>
internal sealed class LoggingQueryStep<TQuery, TResult>(ILogger<LoggingQueryStep<TQuery, TResult>> logger)
    : IQueryPipelineStep<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private static readonly string QueryName = typeof(TQuery).Name;

    public async Task<TResult> HandleAsync(TQuery query, QueryPipelineNext<TQuery, TResult> next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await next(query, cancellationToken);
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Log.Handled(logger, "query", QueryName, elapsedMs);
        return result;
    }
}

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {Kind} {MessageName} in {ElapsedMs:0.0} ms")]
    public static partial void Handled(ILogger logger, string kind, string messageName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Kind} {MessageName} failed after {ElapsedMs:0.0} ms")]
    public static partial void Failed(ILogger logger, Exception exception, string kind, string messageName, double elapsedMs);
}
