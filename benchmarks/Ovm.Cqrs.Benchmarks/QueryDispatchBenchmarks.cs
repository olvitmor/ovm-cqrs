using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs.Benchmarks;

/// <summary>
/// Cost of dispatching through <see cref="IQueryProcessor"/> compared with calling the handler directly.
/// </summary>
[MemoryDiagnoser]
public class QueryDispatchBenchmarks
{
    private readonly Ping _ping = new(42);
    private readonly PingHandler _handler = new();

    private ServiceProvider _noSteps = null!;
    private ServiceProvider _twoSteps = null!;
    private AsyncServiceScope _noStepsScope;
    private AsyncServiceScope _twoStepsScope;
    private IQueryProcessor _noStepsProcessor = null!;
    private IQueryProcessor _twoStepsProcessor = null!;

    [GlobalSetup]
    public void Setup()
    {
        _noSteps = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<PingHandler>())
            .BuildServiceProvider();
        _twoSteps = new ServiceCollection()
            .AddCqrs(cqrs => cqrs
                .AddHandler<PingHandler>()
                .AddQueryPipelineStep(typeof(PassThroughStep<,>))
                .AddQueryPipelineStep(typeof(AnotherPassThroughStep<,>)))
            .BuildServiceProvider();

        _noStepsScope = _noSteps.CreateAsyncScope();
        _twoStepsScope = _twoSteps.CreateAsyncScope();
        _noStepsProcessor = _noStepsScope.ServiceProvider.GetRequiredService<IQueryProcessor>();
        _twoStepsProcessor = _twoStepsScope.ServiceProvider.GetRequiredService<IQueryProcessor>();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _noStepsScope.DisposeAsync();
        await _twoStepsScope.DisposeAsync();
        await _noSteps.DisposeAsync();
        await _twoSteps.DisposeAsync();
    }

    [Benchmark(Baseline = true, Description = "Handler called directly")]
    public Task<int> DirectHandlerCall() => _handler.HandleAsync(_ping, CancellationToken.None);

    [Benchmark(Description = "Explicit, no steps")]
    public Task<int> ExplicitNoSteps() => _noStepsProcessor.ProcessQueryAsync<Ping, int>(_ping);

    [Benchmark(Description = "Inferred, no steps")]
    public Task<int> InferredNoSteps() => _noStepsProcessor.ProcessQueryAsync(_ping);

    [Benchmark(Description = "Explicit, 2 steps")]
    public Task<int> ExplicitTwoSteps() => _twoStepsProcessor.ProcessQueryAsync<Ping, int>(_ping);

    [Benchmark(Description = "Inferred, 2 steps")]
    public Task<int> InferredTwoSteps() => _twoStepsProcessor.ProcessQueryAsync(_ping);

    /// <summary>Closest to a real web request: a new DI scope, so the processor and handler are created each time.</summary>
    [Benchmark(Description = "Inferred, 2 steps, new scope per call")]
    public async Task<int> InferredTwoStepsNewScope()
    {
        await using var scope = _twoSteps.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(_ping);
    }
}

public sealed record Ping(int Value) : IQuery<int>;

public sealed class PingHandler : IQueryHandler<Ping, int>
{
    public Task<int> HandleAsync(Ping query, CancellationToken cancellationToken) => Task.FromResult(query.Value);
}

public sealed class PassThroughStep<TQuery, TResult> : IQueryPipelineStep<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, QueryPipelineNext<TQuery, TResult> next, CancellationToken cancellationToken) =>
        next(query, cancellationToken);
}

public sealed class AnotherPassThroughStep<TQuery, TResult> : IQueryPipelineStep<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, QueryPipelineNext<TQuery, TResult> next, CancellationToken cancellationToken) =>
        next(query, cancellationToken);
}
