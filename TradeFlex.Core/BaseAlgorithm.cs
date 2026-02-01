using System.Threading.Tasks;
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
    public virtual Task InitializeAsync(IAlgorithmContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public abstract Task OnBarAsync(Bar bar);

    /// <inheritdoc />
    public virtual Task OnExitAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public virtual bool OnRiskCheck(Order order) => true;

    /// <summary>
    /// Helper to submit a buy order asynchronously.
    /// </summary>
    /// <param name="symbol">The symbol to buy.</param>
    /// <param name="quantity">The quantity to buy (supports fractional).</param>
    protected async Task BuyAsync(string symbol, decimal quantity)
    {
        var order = new Order(symbol, quantity, 0); // Market order (price 0 placeholder)
        if (OnRiskCheck(order))
        {
            await Broker.SubmitOrderAsync(order);
        }
    }

    /// <summary>
    /// Helper to submit a sell order asynchronously.
    /// </summary>
    /// <param name="symbol">The symbol to sell.</param>
    /// <param name="quantity">The quantity to sell (supports fractional).</param>
    protected async Task SellAsync(string symbol, decimal quantity)
    {
        // We use negative quantity to indicate a sell order.
        var order = new Order(symbol, -quantity, 0);
        if (OnRiskCheck(order))
        {
            await Broker.SubmitOrderAsync(order);
        }
    }
}
