using System.ComponentModel.DataAnnotations;

namespace Ovm.Cqrs.Samples.WebApi.Pipeline;

/// <summary>
/// Validates every command's DataAnnotations attributes before its handler runs.
/// Throws <see cref="ValidationException"/>, which Program.cs turns into a 400 response.
/// </summary>
internal sealed class ValidationCommandStep<TCommand, TResult> : ICommandPipelineStep<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CommandPipelineNext<TCommand, TResult> next, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(command, new ValidationContext(command), validateAllProperties: true);
        return next(command, cancellationToken);
    }
}
