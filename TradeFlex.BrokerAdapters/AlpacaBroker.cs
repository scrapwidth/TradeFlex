using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpacaBroker"/> class.
    /// </summary>
    /// <param name="configuration">The Alpaca configuration.</param>
    public AlpacaBroker(AlpacaConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Create Alpaca client based on paper/live setting
        var secretKey = new SecretKey(configuration.ApiKeyId, configuration.SecretKey);
        
        _client = configuration.UsePaperTrading
            ? Environments.Paper.GetAlpacaTradingClient(secretKey)
            : Environments.Live.GetAlpacaTradingClient(secretKey);

        // Initialize positions and balance from Alpaca account
        SyncAccountState().Wait();

        Console.WriteLine($"[AlpacaBroker] Connected to Alpaca ({(configuration.UsePaperTrading ? "Paper" : "Live")} Trading)");
        Console.WriteLine($"[AlpacaBroker] Initial Cash: {_cashBalance:C}");
    }

    /// <inheritdoc />
    public void SubmitOrder(Order order)
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
                var asset = _client.GetAssetAsync(order.Symbol).Result;
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
                var result = _client.PostOrderAsync(orderRequest).Result;
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
                    
                    var result = _client.PostOrderAsync(orderRequest).Result;
                    Console.WriteLine($"[AlpacaBroker] Integer order submitted: {result.OrderId} - {side} {intQuantity} {order.Symbol} (Status: {result.OrderStatus})");
                }
                else
                {
                    Console.WriteLine($"[AlpacaBroker] Cannot submit integer order: Quantity {absQuantity:F8} rounds to 0.");
                    throw; // Re-throw if we can't handle it
                }
            }

            // Wait a moment for the order to fill
            Task.Delay(1000).Wait();

            // Sync positions after order
            SyncAccountState().Wait();
            
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
    public decimal GetPosition(string symbol)
    {
        return _positions.TryGetValue(symbol, out var quantity) ? quantity : 0m;
    }

    /// <inheritdoc />
    public decimal GetAccountBalance()
    {
        return _cashBalance;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, decimal> GetOpenPositions()
    {
        return new Dictionary<string, decimal>(_positions);
    }

    /// <summary>
    /// Synchronizes positions and account balance from Alpaca.
    /// </summary>
    private async Task SyncAccountState()
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
