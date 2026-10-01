using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs.Tests;

public class CommandDispatchTests
{
    public sealed record CreateUser(string Name) : ICommand<int>;

    public sealed class CreateUserHandler : ICommandHandler<CreateUser, int>
    {
        public Task<int> HandleAsync(CreateUser command, CancellationToken cancellationToken) =>
            Task.FromResult(command.Name.Length);
    }

    [Fact]
    public async Task ProcessCommandAsync_Explicit_ReturnsHandlerResult()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<CreateUserHandler>())
            .BuildServiceProvider();
        var commands = provider.GetRequiredService<ICommandProcessor>();

        var result = await commands.ProcessCommandAsync<CreateUser, int>(new CreateUser("Grace"), TestContext.Current.CancellationToken);

        Assert.Equal(5, result);
    }

    [Fact]
    public async Task ProcessCommandAsync_Inferred_ReturnsHandlerResult()
    {
        await using var provider = new ServiceCollection()
            .AddCqrs(cqrs => cqrs.AddHandler<CreateUserHandler>())
            .BuildServiceProvider();
        var commands = provider.GetRequiredService<ICommandProcessor>();

        var result = await commands.ProcessCommandAsync(new CreateUser("Grace"), TestContext.Current.CancellationToken);

        Assert.Equal(5, result);
    }
}
