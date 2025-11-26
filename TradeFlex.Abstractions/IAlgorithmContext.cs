namespace TradeFlex.Abstractions;

/// <summary>
/// Provides context to the algorithm, such as the broker instance.
/// </summary>
public interface IAlgorithmContext
{
    /// <summary>
    /// Gets the broker used for order execution.
    /// </summary>
    IBroker Broker { get; }
}
