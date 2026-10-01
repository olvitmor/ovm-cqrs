using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs.Tests;

public class RegistrationTests
{
    public sealed record Unhandled : ICommand<int>;

    public sealed record Ping : IQuery<string>;

    public sealed class PingHandlerA : IQueryHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping query, CancellationToken cancellationToken) => Task.FromResult("A");
    }

    public sealed class PingHandlerB : IQueryHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping query, CancellationToken cancellationToken) => Task.FromResult("B");
    }

    [Fact]
    public void TwoHandlersForSameMessage_AddCqrsThrowsDuplicateHandlerException()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<DuplicateHandlerException>(() =>
            services.AddCqrs(cqrs => cqrs.AddHandler<PingHandlerA>().AddHandler<PingHandlerB>()));

        Assert.Equal(typeof(Ping), ex.MessageType);
        Assert.Contains(nameof(PingHandlerA), ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PingHandlerB), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameHandlerRegisteredTwice_ThrowsDuplicateHandlerException()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<DuplicateHandlerException>(() =>
            services.AddCqrs(cqrs => cqrs.AddHandler<PingHandlerA>().AddHandler<PingHandlerA>()));

        Assert.Equal(typeof(Ping), ex.MessageType);
    }

    [Fact]
    public void DuplicateAcrossSeparateAddCqrsCalls_ThrowsDuplicateHandlerException()
    {
        var services = new ServiceCollection().AddCqrs(cqrs => cqrs.AddHandler<PingHandlerA>());

        var ex = Assert.Throws<DuplicateHandlerException>(() =>
            services.AddCqrs(cqrs => cqrs.AddHandler<PingHandlerB>()));

        Assert.Equal(typeof(Ping), ex.MessageType);
    }

    [Fact]
    public void DuplicateOfManuallyRegisteredHandler_ThrowsDuplicateHandlerException()
    {
        var services = new ServiceCollection().AddScoped<IQueryHandler<Ping, string>, PingHandlerA>();

        var ex = Assert.Throws<DuplicateHandlerException>(() =>
            services.AddCqrs(cqrs => cqrs.AddHandler<PingHandlerB>()));

        Assert.Equal(typeof(Ping), ex.MessageType);
    }

    [Fact]
    public async Task MissingHandler_ThrowsHandlerNotFoundException_NamingTheMessage()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(_ => { })
            .BuildServiceProvider();

        var ex = await Assert.ThrowsAsync<HandlerNotFoundException>(() =>
            provider.GetRequiredService<ICommandProcessor>().ProcessCommandAsync(new Unhandled(), TestContext.Current.CancellationToken));

        Assert.Equal(typeof(Unhandled), ex.MessageType);
        Assert.Contains(nameof(Unhandled), ex.Message, StringComparison.Ordinal);
    }
}
