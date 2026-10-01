using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ovm.Cqrs;

/// <summary>
/// Registers Ovm.Cqrs in an <see cref="IServiceCollection"/>.
/// </summary>
public static class CqrsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the command and query processors and applies the given configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures handlers and pipeline steps.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCqrs(this IServiceCollection services, Action<CqrsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.TryAddScoped<ICommandProcessor, CommandProcessor>();
        services.TryAddScoped<IQueryProcessor, QueryProcessor>();
        configure(new CqrsBuilder(services));
        return services;
    }
}
