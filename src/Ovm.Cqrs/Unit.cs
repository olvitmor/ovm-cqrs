namespace Ovm.Cqrs;

/// <summary>
/// The result of a command that returns nothing. There is exactly one value: <see cref="Value"/>.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// The single <see cref="Unit"/> value.
    /// </summary>
    public static readonly Unit Value;

    /// <inheritdoc />
    public override string ToString() => "()";
}
