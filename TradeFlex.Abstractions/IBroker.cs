using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradeFlex.Abstractions;

/// <summary>
/// Represents a broker that can accept orders and provide account information.
/// </summary>
public interface IBroker
{
    /// <summary>
    /// Submits an order to the broker asynchronously.
    /// </summary>
    /// <param name="order">The order to submit.</param>
    Task SubmitOrderAsync(Order order);

    /// <summary>
    /// Gets the current position for a symbol (supports fractional quantities) asynchronously.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The current position (positive for long, negative for short).</returns>
    Task<decimal> GetPositionAsync(string symbol);

    /// <summary>
    /// Gets the current cash balance of the account asynchronously.
    /// </summary>
    /// <returns>The cash balance.</returns>
    Task<decimal> GetAccountBalanceAsync();

    /// <summary>
    /// Gets all open positions (supports fractional quantities) asynchronously.
    /// </summary>
    /// <returns>A dictionary of symbol to quantity.</returns>
    Task<IReadOnlyDictionary<string, decimal>> GetOpenPositionsAsync();
}
