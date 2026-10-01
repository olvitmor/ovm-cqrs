using Microsoft.Extensions.DependencyInjection;
using Ovm.Cqrs.Tests.Fixtures;

namespace Ovm.Cqrs.Tests;

public class ScanningTests
{
    private static async Task AssertScannedHandlersWork(ServiceProvider provider)
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal("scanned Ada", await provider.GetRequiredService<IQueryProcessor>().ProcessQueryAsync(new ScannedQuery("Ada"), ct));
        Assert.Equal(42, await provider.GetRequiredService<ICommandProcessor>().ProcessCommandAsync(new ScannedCommand(21), ct));
    }

    [Fact]
    public async Task AddHandlersFromAssembly_RegistersConcreteHandlers()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandlersFromAssembly(typeof(ScannedQuery).Assembly))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        await AssertScannedHandlersWork(provider);
    }

    [Fact]
    public async Task AddHandlersFromAssemblyContaining_RegistersConcreteHandlers()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandlersFromAssemblyContaining<ScannedQuery>())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        await AssertScannedHandlersWork(provider);
    }
}
