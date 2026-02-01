using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alpaca.Markets;
using TradeFlex.Abstractions;

namespace TradeFlex.BrokerAdapters;

/// <summary>
/// Alpaca broker implementation for paper and live trading.
/// </summary>
public class AlpacaBroker : IBroker
{
    private readonly IAlpacaTradingClient _client;
    private readonly Dictionary<string, decimal> _positions = new();
    private decimal _cashBalance;
    private readonly bool _usePaperTrading;

    /// <summary>
    /// Creates an AlpacaBroker instance asynchronously.
    /// </summary>
    /// <param name="configuration">The Alpaca configuration.</param>
    /// <returns>A configured AlpacaBroker instance.</returns>
    public static async Task<AlpacaBroker> CreateAsync(AlpacaConfiguration configuration)
    {
        var broker = new AlpacaBroker(configuration);
        await broker.SyncAccountStateAsync();
        Console.WriteLine($"[AlpacaBroker] Connected to Alpaca ({(configuration.UsePaperTrading ? "Paper" : "Live")} Trading)");
        Console.WriteLine($"[AlpacaBroker] Initial Cash: {broker._cashBalance:C}");
        return broker;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpacaBroker"/> class.
    /// Use CreateAsync for proper initialization.
    /// </summary>
    /// <param name="configuration">The Alpaca configuration.</param>
    private AlpacaBroker(AlpacaConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _usePaperTrading = configuration.UsePaperTrading;

        // Create Alpaca client based on paper/live setting
        var secretKey = new SecretKey(configuration.ApiKeyId, configuration.SecretKey);

        _client = configuration.UsePaperTrading
            ? Environments.Paper.GetAlpacaTradingClient(secretKey)
            : Environments.Live.GetAlpacaTradingClient(secretKey);
    }

    /// <inheritdoc />
    public async Task SubmitOrderAsync(Order order)
    {
        try
        {
            // Determine order side using fully qualified type to avoid ambiguity
            var side = order.Quantity > 0 ? Alpaca.Markets.OrderSide.Buy : Alpaca.Markets.OrderSide.Sell;
            var absQuantity = Math.Abs(order.Quantity);

            // Round to 8 decimal places (standard for crypto)
            absQuantity = Math.Round(absQuantity, 8);

            // Fetch asset details to check fractionability
            try
            {
                var asset = await _client.GetAssetAsync(order.Symbol);
                Console.WriteLine($"[AlpacaBroker] Asset {asset.Symbol}: Fractionable={asset.Fractionable}, Tradable={asset.IsTradable}, Marginable={asset.Marginable}");

                if (asset.Fractionable == false && absQuantity % 1 != 0)
                {
                    Console.WriteLine($"[AlpacaBroker] WARNING: Asset {asset.Symbol} is NOT fractionable, but order quantity is {absQuantity}. Order will likely fail.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlpacaBroker] Warning: Could not fetch asset details: {ex.Message}");
            }

            Console.WriteLine($"[AlpacaBroker] Attempting to {side} {absQuantity:F8} {order.Symbol}");

            try
            {
                // Create order request explicitly to control TimeInForce
                var orderRequest = new NewOrderRequest(
                    order.Symbol,
                    OrderQuantity.Fractional(absQuantity),
                    side,
                    OrderType.Market,
                    TimeInForce.Gtc
                );

                // Submit order to Alpaca
                var result = await _client.PostOrderAsync(orderRequest);
                Console.WriteLine($"[AlpacaBroker] Order submitted: {result.OrderId} - {side} {absQuantity:F8} {order.Symbol} (Status: {result.OrderStatus})");
            }
            catch (Exception ex) when (ex.Message.Contains("qty must be integer") || (ex.InnerException?.Message.Contains("qty must be integer") ?? false))
            {
                // Fallback for accounts without fractional trading enabled
                Console.WriteLine("[AlpacaBroker] Fractional order failed (qty must be integer).");
                Console.WriteLine("[AlpacaBroker] TIP: Try RESETTING your Paper Account in the Alpaca Dashboard to fix this.");
                Console.WriteLine("[AlpacaBroker] Attempting integer quantity fallback...");

                var intQuantity = (long)Math.Floor(absQuantity);
                if (intQuantity > 0)
                {
                    var orderRequest = new NewOrderRequest(
                        order.Symbol,
                        OrderQuantity.FromInt64(intQuantity),
                        side,
                        OrderType.Market,
                        TimeInForce.Gtc
                    );

                    var result = await _client.PostOrderAsync(orderRequest);
                    Console.WriteLine($"[AlpacaBroker] Integer order submitted: {result.OrderId} - {side} {intQuantity} {order.Symbol} (Status: {result.OrderStatus})");
                }
                else
                {
                    Console.WriteLine($"[AlpacaBroker] Cannot submit integer order: Quantity {absQuantity:F8} rounds to 0.");
                    throw; // Re-throw if we can't handle it
                }
            }

            // Wait a moment for the order to fill
            await Task.Delay(1000);

            // Sync positions after order
            await SyncAccountStateAsync();

            Console.WriteLine($"[AlpacaBroker] Current cash: {_cashBalance:C}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[AlpacaBroker] Order submission failed: {ex.Message}");

            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"[AlpacaBroker] Inner exception: {ex.InnerException.Message}");
            }

            Console.WriteLine($"[AlpacaBroker] Continuing despite order failure...");
        }
    }

    /// <inheritdoc />
    public Task<decimal> GetPositionAsync(string symbol)
    {
        var position = _positions.TryGetValue(symbol, out var quantity) ? quantity : 0m;
        return Task.FromResult(position);
    }

    /// <inheritdoc />
    public Task<decimal> GetAccountBalanceAsync()
    {
        return Task.FromResult(_cashBalance);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, decimal>> GetOpenPositionsAsync()
    {
        IReadOnlyDictionary<string, decimal> result = new Dictionary<string, decimal>(_positions);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Synchronizes positions and account balance from Alpaca.
    /// </summary>
    private async Task SyncAccountStateAsync()
    {
        try
        {
            // Fetch account info
            var account = await _client.GetAccountAsync();
            _cashBalance = account.TradableCash;

            // Fetch positions
            var positions = await _client.ListPositionsAsync();

            _positions.Clear();
            foreach (var position in positions)
            {
                _positions[position.Symbol] = position.Quantity;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[AlpacaBroker] Failed to sync account state: {ex.Message}");
            throw;
        }
    }
}
