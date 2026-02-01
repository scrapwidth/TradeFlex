using TradeFlex.Abstractions;

namespace TradeFlex.Core;

/// <summary>
/// Base class implementing <see cref="ITradingAlgorithm"/> with helper methods.
/// </summary>
public abstract class BaseAlgorithm : ITradingAlgorithm
{
    private IAlgorithmContext? _context;

    /// <summary>
    /// Gets the broker instance.
    /// </summary>
    protected IBroker Broker => _context?.Broker ?? throw new InvalidOperationException("Algorithm not initialized.");

    /// <inheritdoc />
    public virtual void Initialize(IAlgorithmContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public abstract void OnBar(Bar bar);

    /// <inheritdoc />
    public virtual void OnExit() { }

    /// <inheritdoc />
    public virtual bool OnRiskCheck(Order order) => true;

    /// <summary>
    /// Helper to submit a buy order.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    /// <param name="quantity">The quantity to buy (supports fractional).</param>
    protected void Buy(string symbol, decimal quantity)
    {
        var order = new Order(symbol, quantity, 0); // Market order (price 0 placeholder)
        if (OnRiskCheck(order))
        {
            Broker.SubmitOrder(order);
        }
    }

    /// <summary>
    /// Helper to submit a sell order.
    /// </summary>
    /// <param name="symbol">The symbol to sell.</param>
    /// <param name="quantity">The quantity to sell (supports fractional).</param>
    protected void Sell(string symbol, decimal quantity)
    {
        // We use negative quantity to indicate a sell order.
        var order = new Order(symbol, -quantity, 0);
        if (OnRiskCheck(order))
        {
            Broker.SubmitOrder(order);
        }
    }
}
