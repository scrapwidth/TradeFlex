using TradeFlex.Abstractions;

namespace TradeFlex.Core;

/// <summary>
/// Base class implementing <see cref="ITradingAlgorithm"/> with no-op defaults.
/// </summary>
public abstract class BaseAlgorithm : ITradingAlgorithm
{
    /// <inheritdoc />
    public virtual void Initialize() { }

    /// <inheritdoc />
    public abstract void OnBar(Bar bar);

    /// <inheritdoc />
    public abstract void OnEntry(Order order);

    /// <inheritdoc />
    public abstract void OnExit();

    /// <inheritdoc />
    public abstract bool OnRiskCheck(Order order);
}
