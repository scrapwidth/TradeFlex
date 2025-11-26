using System.Collections.Generic;

namespace TradeFlex.Abstractions;

/// <summary>
/// Represents a broker that can accept orders and provide account information.
/// </summary>
public interface IBroker
{
    /// <summary>
    /// Submits an order to the broker.
    /// </summary>
    /// <param name="order">The order to submit.</param>
    void SubmitOrder(Order order);

    /// <summary>
    /// Gets the current position for a symbol (supports fractional quantities).
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The current position (positive for long, negative for short).</returns>
    decimal GetPosition(string symbol);

    /// <summary>
    /// Gets the current cash balance of the account.
    /// </summary>
    /// <returns>The cash balance.</returns>
    decimal GetAccountBalance();

    /// <summary>
    /// Gets all open positions (supports fractional quantities).
    /// </summary>
    /// <returns>A dictionary of symbol to quantity.</returns>
    IReadOnlyDictionary<string, decimal> GetOpenPositions();
}
