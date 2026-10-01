using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs.Tests;

public class PipelineTests
{
    public sealed class Trace : List<string>;

    public sealed record GetValue : IQuery<string>;

    public sealed class GetValueHandler(Trace trace) : IQueryHandler<GetValue, string>
    {
        public Task<string> HandleAsync(GetValue query, CancellationToken cancellationToken)
        {
            trace.Add("handler");
            return Task.FromResult("value");
        }
    }

    public abstract class TracingQueryStep<TQuery, TResult>(Trace trace, string name) : IQueryPipelineStep<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public async Task<TResult> HandleAsync(TQuery query, QueryPipelineNext<TQuery, TResult> next, CancellationToken cancellationToken)
        {
            trace.Add($"{name}:before");
            var result = await next(query, cancellationToken);
            trace.Add($"{name}:after");
            return result;
        }
    }

    public sealed class OuterQueryStep<TQuery, TResult>(Trace trace) : TracingQueryStep<TQuery, TResult>(trace, "outer")
        where TQuery : IQuery<TResult>;

    public sealed class InnerQueryStep<TQuery, TResult>(Trace trace) : TracingQueryStep<TQuery, TResult>(trace, "inner")
        where TQuery : IQuery<TResult>;

    public sealed class ShortCircuitStep : IQueryPipelineStep<GetValue, string>
    {
        public Task<string> HandleAsync(GetValue query, QueryPipelineNext<GetValue, string> next, CancellationToken cancellationToken) =>
            Task.FromResult("cached");
    }

    public sealed record SetValue : ICommand;

    public sealed class SetValueHandler(Trace trace) : ICommandHandler<SetValue>
    {
        public Task HandleAsync(SetValue command, CancellationToken cancellationToken)
        {
            trace.Add("command-handler");
            return Task.CompletedTask;
        }
    }

    public sealed class TracingCommandStep<TCommand, TResult>(Trace trace) : ICommandPipelineStep<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        public async Task<TResult> HandleAsync(TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken)
        {
            trace.Add("command-step:before");
            var result = await next(command, cancellationToken);
            trace.Add("command-step:after");
            return result;
        }
    }

    private static ServiceProvider BuildProvider(Trace trace, Action<CqrsBuilder> configure) =>
        new ServiceCollection()
            .AddSingleton(trace)
            .AddCqrs(cqrs => configure(cqrs.AddHandler<GetValueHandler>()))
            .BuildServiceProvider();

    [Fact]
    public async Task QueryStep_WrapsHandler()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace, cqrs => cqrs.AddQueryPipelineStep(typeof(OuterQueryStep<,>)));

        var result = await provider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(new GetValue(), TestContext.Current.CancellationToken);

        Assert.Equal("value", result);
        Assert.Equal(["outer:before", "handler", "outer:after"], trace);
    }

    [Fact]
    public async Task QuerySteps_RunInRegistrationOrder_FirstRegisteredOutermost()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace, cqrs => cqrs
            .AddQueryPipelineStep(typeof(OuterQueryStep<,>))
            .AddQueryPipelineStep(typeof(InnerQueryStep<,>)));

        await provider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(new GetValue(), TestContext.Current.CancellationToken);

        Assert.Equal(["outer:before", "inner:before", "handler", "inner:after", "outer:after"], trace);
    }

    [Fact]
    public async Task QueryStep_CanShortCircuit_HandlerNotCalled()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace, cqrs => cqrs.AddQueryPipelineStep(typeof(ShortCircuitStep)));

        var result = await provider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(new GetValue(), TestContext.Current.CancellationToken);

        Assert.Equal("cached", result);
        Assert.Empty(trace);
    }

    [Fact]
    public async Task CommandAndQuerySteps_OnlyRunForTheirOwnSide()
    {
        var trace = new Trace();
        await using var provider = BuildProvider(trace, cqrs => cqrs
            .AddHandler<SetValueHandler>()
            .AddCommandPipelineStep(typeof(TracingCommandStep<,>))
            .AddQueryPipelineStep(typeof(OuterQueryStep<,>)));
        var ct = TestContext.Current.CancellationToken;

        await provider.GetRequiredService<ICommandProcessor>().ProcessCommandAsync(new SetValue(), ct);
        Assert.Equal(["command-step:before", "command-handler", "command-step:after"], trace);

        trace.Clear();
        await provider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(new GetValue(), ct);
        Assert.Equal(["outer:before", "handler", "outer:after"], trace);
    }
}
