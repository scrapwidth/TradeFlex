using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradeFlex.Abstractions;

namespace TradeFlex.Core;

/// <summary>
/// A real-time data feed from Coinbase Pro via WebSocket.
/// </summary>
public class CoinbaseDataFeed : IMarketDataFeed
{
    private readonly string _symbol;
    private readonly Uri _wsUri = new("wss://ws-feed.exchange.coinbase.com");

    public CoinbaseDataFeed(string symbol)
    {
        // Coinbase uses "BTC-USD" format. Ensure we have a dash if needed, or assume caller passes correct format.
        // We'll trust the caller for now but maybe normalize later.
        _symbol = symbol;
    }

    public async IAsyncEnumerable<Bar> GetFeedAsync(CancellationToken cancellationToken = default)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(_wsUri, cancellationToken);
        Console.WriteLine($"Connected to Coinbase Feed for {_symbol}");

        // Subscribe
        var subscribeMsg = new
        {
            type = "subscribe",
            product_ids = new[] { _symbol },
            channels = new[] { "ticker" }
        };
        
        var json = JsonSerializer.Serialize(subscribeMsg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

        var buffer = new byte[1024 * 4];
        
        // Aggregation state
        Bar? currentBar = null;
        DateTime nextBarTime = DateTime.MinValue;

        while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            
            // Parse JSON
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "ticker")
            {
                if (root.TryGetProperty("price", out var priceProp) && 
                    root.TryGetProperty("time", out var timeProp))
                {
                    if (decimal.TryParse(priceProp.GetString(), out var price) && 
                        root.TryGetProperty("last_size", out var sizeProp) &&
                        decimal.TryParse(sizeProp.GetString(), out var size))
                    {
                        var time = timeProp.GetDateTime();
                        
                        // Simple 1-minute aggregation logic
                        // Align to minute boundary
                        var barTime = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, DateTimeKind.Utc);

                        if (currentBar == null)
                        {
                            currentBar = new Bar(_symbol, barTime, price, price, price, price, (long)size);
                            nextBarTime = barTime.AddMinutes(1);
                        }
                        else if (time >= nextBarTime)
                        {
                            // Yield the completed bar
                            yield return currentBar;
                            
                            // Start new bar
                            currentBar = new Bar(_symbol, nextBarTime, price, price, price, price, (long)size);
                            nextBarTime = nextBarTime.AddMinutes(1);
                        }
                        else
                        {
                            // Update current bar
                            currentBar = currentBar with
                            {
                                High = Math.Max(currentBar.High, price),
                                Low = Math.Min(currentBar.Low, price),
                                Close = price,
                                Volume = currentBar.Volume + (long)size
                            };
                            
                            // Optional: Yield intermediate updates? 
                            // For true "OnBar" backtest semantics, we only yield when closed.
                            // But for shadow trading, waiting 1 minute for an update is boring and risky.
                            // Let's yield the "developing" bar so the algo can react?
                            // Or better: The algo interface expects a *completed* bar usually.
                            // However, many live systems send "tick" updates via OnBar or a separate OnTick.
                            // Since we only have OnBar, let's yield the developing bar but maybe mark it?
                            // For this demo, let's just yield every update as a "Bar" representing the current state.
                            // This effectively makes it a "Tick Bar".
                            yield return currentBar;
                        }
                    }
                }
            }
        }
    }
}
