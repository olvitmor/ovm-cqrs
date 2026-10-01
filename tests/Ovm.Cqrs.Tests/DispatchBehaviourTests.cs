using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs.Tests;

public class DispatchBehaviourTests
{
    public sealed record Probe : IQuery<CancellationToken>;

    public sealed class ProbeHandler : IQueryHandler<Probe, CancellationToken>
    {
        public Task<CancellationToken> HandleAsync(Probe query, CancellationToken cancellationToken) =>
            Task.FromResult(cancellationToken);
    }

    public sealed record Explode(Exception Exception) : ICommand<int>;

    public sealed class ExplodeHandler : ICommandHandler<Explode, int>
    {
        public Task<int> HandleAsync(Explode command, CancellationToken cancellationToken) => throw command.Exception;
    }

    [Fact]
    public async Task CancellationToken_ReachesHandler()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<ProbeHandler>())
            .BuildServiceProvider();
        using var cts = new CancellationTokenSource();

        var received = await provider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(new Probe(), cts.Token);

        Assert.Equal(cts.Token, received);
    }

    [Fact]
    public async Task HandlerException_PropagatesUnwrapped()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<ExplodeHandler>())
            .BuildServiceProvider();
        var boom = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<ICommandProcessor>().ProcessCommandAsync(new Explode(boom), TestContext.Current.CancellationToken));

        Assert.Same(boom, thrown);
    }
}
