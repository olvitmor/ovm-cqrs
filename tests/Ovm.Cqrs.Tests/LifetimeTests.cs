using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs.Tests;

public class LifetimeTests
{
    public sealed record WhoAmI : IQuery<Guid>;

    public sealed class WhoAmIHandler : IQueryHandler<WhoAmI, Guid>
    {
        private readonly Guid _instanceId = Guid.NewGuid();

        public Task<Guid> HandleAsync(WhoAmI query, CancellationToken cancellationToken) => Task.FromResult(_instanceId);
    }

    private static async Task<(Guid First, Guid Second, Guid OtherScope)> DispatchTwiceAndInAnotherScope(ServiceProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;
        Guid first, second, otherScope;
        await using (var scope = provider.CreateAsyncScope())
        {
            var queries = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();
            first = await queries.ProcessQueryAsync(new WhoAmI(), ct);
            second = await queries.ProcessQueryAsync(new WhoAmI(), ct);
        }

        await using (var scope = provider.CreateAsyncScope())
            otherScope = await scope.ServiceProvider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(new WhoAmI(), ct);

        return (first, second, otherScope);
    }

    [Fact]
    public async Task Handlers_AreScopedByDefault()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<WhoAmIHandler>())
            .BuildServiceProvider();

        var (first, second, otherScope) = await DispatchTwiceAndInAnotherScope(provider);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherScope);
    }

    [Fact]
    public async Task Handlers_HonourLifetimeOverride()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<WhoAmIHandler>(ServiceLifetime.Transient))
            .BuildServiceProvider();

        var (first, second, _) = await DispatchTwiceAndInAnotherScope(provider);

        Assert.NotEqual(first, second);
    }
}
