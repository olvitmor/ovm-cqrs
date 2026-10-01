using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs.Tests;

public class QueryDispatchTests
{
    public sealed record GetGreeting(string Name) : IQuery<string>;

    public sealed class GetGreetingHandler : IQueryHandler<GetGreeting, string>
    {
        public Task<string> HandleAsync(GetGreeting query, CancellationToken cancellationToken) =>
            Task.FromResult($"Hello, {query.Name}!");
    }

    [Fact]
    public async Task ProcessQueryAsync_Explicit_ReturnsHandlerResult()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<GetGreetingHandler>())
            .BuildServiceProvider();
        var queries = provider.GetRequiredService<IQueryProcessor>();

        var result = await queries.ProcessQueryAsync<GetGreeting, string>(new GetGreeting("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada!", result);
    }

    [Fact]
    public async Task ProcessQueryAsync_Inferred_ReturnsHandlerResult()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<GetGreetingHandler>())
            .BuildServiceProvider();
        var queries = provider.GetRequiredService<IQueryProcessor>();

        var result = await queries.ProcessQueryAsync(new GetGreeting("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada!", result);
    }
}
