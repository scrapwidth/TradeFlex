using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradeFlex.Abstractions;

namespace TradeFlex.Core;

/// <summary>
/// A simulated broker for backtesting and paper trading.
/// </summary>
public class PaperBroker : IBroker
{
    private readonly Dictionary<string, decimal> _positions = new();
    private readonly Dictionary<string, decimal> _lastPrices = new();
    private decimal _cash;
    private readonly decimal _feePercentage;
    private readonly List<Trade> _trades = new();
    private readonly bool _verbose;

    /// <summary>
    /// Gets the list of executed trades.
    /// </summary>
    public IReadOnlyList<Trade> Trades => _trades;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaperBroker"/> class.
    /// </summary>
    /// <param name="initialCash">The starting cash balance.</param>
    /// <param name="feePercentage">The fee percentage (e.g., 0.005 for 0.5%).</param>
    /// <param name="verbose">Whether to print trade logs to console.</param>
    public PaperBroker(decimal initialCash, decimal feePercentage = 0.005m, bool verbose = true)
    {
        _cash = initialCash;
        _feePercentage = feePercentage;
        _verbose = verbose;
    }

    /// <summary>
    /// Updates the latest market price for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="price">The current price.</param>
    public void UpdatePrice(string symbol, decimal price)
    {
        _lastPrices[symbol] = price;
    }

    /// <inheritdoc />
    public Task SubmitOrderAsync(Order order)
    {
        var fillPrice = order.Price;

        // If market order (price 0), use the last known market price
        if (fillPrice <= 0)
        {
            if (_lastPrices.TryGetValue(order.Symbol, out var marketPrice))
            {
                fillPrice = marketPrice;
            }
            else
            {
                if (_verbose) Console.WriteLine($"[PaperBroker] Warning: No market price for {order.Symbol}. Cannot fill market order.");
                return Task.CompletedTask;
            }
        }

        var notionalValue = fillPrice * Math.Abs(order.Quantity);
        var fee = notionalValue * _feePercentage;
        var totalCost = notionalValue + fee;

        // Basic validation for buys
        if (order.Quantity > 0 && _cash < totalCost)
        {
            if (_verbose) Console.WriteLine($"[PaperBroker] Insufficient funds. Need {totalCost:C}, have {_cash:C}");
            return Task.CompletedTask;
        }

        // Update Cash (always deduct fee)
        // Buying (Qty > 0) decreases cash by cost + fee
        // Selling (Qty < 0) increases cash by proceeds - fee
        if (order.Quantity > 0)
        {
            _cash -= totalCost;
        }
        else
        {
            _cash += notionalValue - fee;
        }

        // Update Position
        if (!_positions.ContainsKey(order.Symbol))
        {
            _positions[order.Symbol] = 0;
        }
        _positions[order.Symbol] += order.Quantity;

        // Record Trade
        var side = order.Quantity > 0 ? OrderSide.Buy : OrderSide.Sell;
        var trade = new Trade(order.Symbol, Math.Abs(order.Quantity), fillPrice, side);
        _trades.Add(trade);

        if (_verbose) Console.WriteLine($"[PaperBroker] Filled {side} {Math.Abs(order.Quantity):F8} {order.Symbol} @ {fillPrice:F2}. Fee: {fee:F2}. Cash: {_cash:F2}");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<decimal> GetPositionAsync(string symbol)
    {
        var position = _positions.TryGetValue(symbol, out var qty) ? qty : 0;
        return Task.FromResult(position);
    }

    /// <inheritdoc />
    public Task<decimal> GetAccountBalanceAsync()
    {
        return Task.FromResult(_cash);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, decimal>> GetOpenPositionsAsync()
    {
        IReadOnlyDictionary<string, decimal> result = new Dictionary<string, decimal>(_positions);
        return Task.FromResult(result);
    }
}
