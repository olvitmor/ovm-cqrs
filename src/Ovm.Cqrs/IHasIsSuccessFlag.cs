namespace Ovm.Cqrs;

/// <summary>
/// Optional interface for results that can report failure without throwing.
/// Pipeline steps can inspect it; for example, the EF Core transaction step rolls back when
/// <see cref="IsSuccess"/> is <see langword="false"/>.
/// </summary>
public interface IHasIsSuccessFlag
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    bool IsSuccess { get; }
}
