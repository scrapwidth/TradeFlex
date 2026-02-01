namespace TradeFlex.Abstractions;

/// <summary>
/// Exposes the primary hooks required for a trading algorithm.
/// </summary>
public interface ITradingAlgorithm
{
    /// <summary>
    /// Called once before any bars are processed.
    /// </summary>
    /// <param name="context">The algorithm context.</param>
    void Initialize(IAlgorithmContext context);

    /// <summary>
    /// Invoked for each incoming bar of market data.
    /// </summary>
    /// <param name="bar">The latest bar.</param>
    void OnBar(Bar bar);

    /// <summary>
    /// Called when the algorithm shuts down.
    /// </summary>
    void OnExit();

    /// <summary>
    /// Performs a risk check before an order is submitted.
    /// </summary>
    /// <param name="order">The order to validate.</param>
    /// <returns>True if the order passes risk controls; otherwise, false.</returns>
    bool OnRiskCheck(Order order);
}

/// <summary>
/// Represents a single bar of market data.
/// </summary>
/// <param name="Symbol">The symbol of the instrument.</param>
/// <param name="Timestamp">The time of the bar.</param>
/// <param name="Open">The opening price.</param>
/// <param name="High">The highest price.</param>
/// <param name="Low">The lowest price.</param>
/// <param name="Close">The closing price.</param>
/// <param name="Volume">The traded volume.</param>
public record Bar(string Symbol, DateTime Timestamp, decimal Open, decimal High, decimal Low, decimal Close, long Volume);

/// <summary>
/// Represents an order to buy or sell a security.
/// </summary>
/// <param name="Symbol">The symbol to trade.</param>
/// <param name="Quantity">The quantity (positive for buy, negative for sell). Supports fractional quantities.</param>
/// <param name="Price">The limit price, or 0 for market order.</param>
public record Order(string Symbol, decimal Quantity, decimal Price);

/// <summary>
/// Direction for an order or trade.
/// </summary>
public enum OrderSide
{
    /// <summary>
    /// A buy order.
    /// </summary>
    Buy,

    /// <summary>
    /// A sell order.
    /// </summary>
    Sell
}

/// <summary>
/// Represents an executed trade.
/// </summary>
/// <param name="Symbol">The traded instrument.</param>
/// <param name="Quantity">The number of shares or contracts.</param>
/// <param name="Price">The execution price.</param>
/// <param name="Side">Whether the trade was a buy or sell.</param>
public record Trade(string Symbol, decimal Quantity, decimal Price, OrderSide Side);
