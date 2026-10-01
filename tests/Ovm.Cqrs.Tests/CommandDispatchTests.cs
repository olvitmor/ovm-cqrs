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

    public sealed record DeactivateUser(int UserId) : ICommand;

    public sealed class DeactivatedUsers : List<int>;

    public sealed class DeactivateUserHandler(DeactivatedUsers deactivated) : ICommandHandler<DeactivateUser>
    {
        public Task HandleAsync(DeactivateUser command, CancellationToken cancellationToken)
        {
            deactivated.Add(command.UserId);
            return Task.CompletedTask;
        }
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

    [Fact]
    public async Task ProcessCommandAsync_VoidCommand_RunsHandlerAndReturnsUnit()
    {
        var deactivated = new DeactivatedUsers();
        await using var provider = new ServiceCollection()
            .AddSingleton(deactivated)
            .AddCqrs(cqrs => cqrs.AddHandler<DeactivateUserHandler>())
            .BuildServiceProvider();
        var commands = provider.GetRequiredService<ICommandProcessor>();

        var result = await commands.ProcessCommandAsync(new DeactivateUser(42), TestContext.Current.CancellationToken);

        Assert.Equal(Unit.Value, result);
        Assert.Equal([42], deactivated);
    }
}
