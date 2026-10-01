using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Ovm.Cqrs;

/// <summary>
/// Configures handlers and pipeline steps. Obtained from
/// <see cref="CqrsServiceCollectionExtensions.AddCqrs"/>; add-on packages extend it with extension methods.
/// </summary>
public sealed class CqrsBuilder
{
    private static readonly Type[] HandlerInterfaces = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    private const DynamicallyAccessedMemberTypes HandlerMembers =
        DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors;

    private const string ScanningMessage =
        "Assembly scanning finds handlers via reflection, which trimming can break. Use AddHandler<THandler>() in trimmed or Native AOT apps.";

    private readonly Dictionary<Type, Type> _handlers = [];

    internal CqrsBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// The service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers a single handler for every handler interface it implements. Needs no assembly scanning,
    /// so it is safe for trimmed and Native AOT apps.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="lifetime">The handler lifetime. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="DuplicateHandlerException">A different handler is already registered for the same message.</exception>
    public CqrsBuilder AddHandler<[DynamicallyAccessedMembers(HandlerMembers)] THandler>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where THandler : class =>
        AddHandler(typeof(THandler), lifetime);

    /// <summary>
    /// Registers every concrete command and query handler found in <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">The handler lifetime. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="DuplicateHandlerException">Two handlers are registered for the same message.</exception>
    [RequiresUnreferencedCode(ScanningMessage)]
    public CqrsBuilder AddHandlersFromAssembly(Assembly assembly, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }))
            AddHandler(type, lifetime);

        return this;
    }

    /// <summary>
    /// Registers every concrete command and query handler in the assembly that contains <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Any type from the assembly to scan.</typeparam>
    /// <param name="lifetime">The handler lifetime. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="DuplicateHandlerException">Two handlers are registered for the same message.</exception>
    [RequiresUnreferencedCode(ScanningMessage)]
    public CqrsBuilder AddHandlersFromAssemblyContaining<T>(ServiceLifetime lifetime = ServiceLifetime.Scoped) =>
        AddHandlersFromAssembly(typeof(T).Assembly, lifetime);

    private CqrsBuilder AddHandler([DynamicallyAccessedMembers(HandlerMembers)] Type handlerType, ServiceLifetime lifetime)
    {
        foreach (var serviceType in handlerType.GetInterfaces()
                     .Where(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition())))
        {
            if (_handlers.TryGetValue(serviceType, out var existing))
                throw new DuplicateHandlerException(serviceType.GetGenericArguments()[0], existing, handlerType);

            _handlers.Add(serviceType, handlerType);
            Services.Add(new ServiceDescriptor(serviceType, handlerType, lifetime));
        }

        return this;
    }

    /// <summary>
    /// Adds a command pipeline step. An open generic type (e.g. <c>typeof(AuthorizationStep&lt;,&gt;)</c>) runs for every command;
    /// a closed type runs only for the commands it implements <see cref="ICommandPipelineStep{TCommand, TResult}"/> for.
    /// Steps run in registration order: the first registered is the outermost.
    /// </summary>
    /// <param name="stepType">The step type.</param>
    /// <returns>This builder.</returns>
    public CqrsBuilder AddCommandPipelineStep([DynamicallyAccessedMembers(HandlerMembers)] Type stepType) =>
        AddPipelineStep(typeof(ICommandPipelineStep<,>), stepType);

    /// <summary>
    /// Adds a query pipeline step. An open generic type (e.g. <c>typeof(CachingStep&lt;,&gt;)</c>) runs for every query;
    /// a closed type runs only for the queries it implements <see cref="IQueryPipelineStep{TQuery, TResult}"/> for.
    /// Steps run in registration order: the first registered is the outermost.
    /// </summary>
    /// <param name="stepType">The step type.</param>
    /// <returns>This builder.</returns>
    public CqrsBuilder AddQueryPipelineStep([DynamicallyAccessedMembers(HandlerMembers)] Type stepType) =>
        AddPipelineStep(typeof(IQueryPipelineStep<,>), stepType);

    private CqrsBuilder AddPipelineStep(Type stepInterface, [DynamicallyAccessedMembers(HandlerMembers)] Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        if (stepType.IsGenericTypeDefinition)
        {
            Services.AddScoped(stepInterface, stepType);
            return this;
        }

        foreach (var serviceType in stepType.GetInterfaces()
                     .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == stepInterface))
            Services.AddScoped(serviceType, stepType);

        return this;
    }
}
