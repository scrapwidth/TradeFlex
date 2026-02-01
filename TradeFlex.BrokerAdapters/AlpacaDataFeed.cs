using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alpaca.Markets;
using TradeFlex.Abstractions;

namespace TradeFlex.BrokerAdapters;

/// <summary>
/// A real-time data feed from Alpaca via WebSocket.
/// </summary>
public class AlpacaDataFeed : IMarketDataFeed, IDisposable
{
    private readonly string _symbol;
    private readonly IAlpacaDataStreamingClient _client;
    private readonly AlpacaConfiguration _config;

    public AlpacaDataFeed(string symbol, AlpacaConfiguration config)
    {
        _symbol = symbol;
        _config = config;

        var secretKey = new SecretKey(config.ApiKeyId, config.SecretKey);
        
        // Use IEX for free paper trading data, or SIP if user has paid sub (not handled here yet, defaulting to IEX)
        // Note: Alpaca has different streams for IEX (free) vs SIP (paid).
        // For simplicity in this MVP, we'll use the IEX stream which is standard for paper.
        _client = Environments.Paper.GetAlpacaDataStreamingClient(secretKey);
    }

    public async IAsyncEnumerable<Bar> GetFeedAsync(CancellationToken cancellationToken = default)
    {
        // Connect
        await _client.ConnectAndAuthenticateAsync(cancellationToken);
        Console.WriteLine($"[AlpacaDataFeed] Connected to Alpaca Stream for {_symbol}");

        // Channel to bridge callback-based Alpaca client to IAsyncEnumerable
        var channel = System.Threading.Channels.Channel.CreateUnbounded<Bar>();

        // Subscribe to minute bars
        var subscription = _client.GetMinuteBarSubscription(_symbol);
        subscription.Received += (bar) =>
        {
            var tradeFlexBar = new Bar(
                bar.Symbol,
                bar.TimeUtc,
                bar.Open,
                bar.High,
                bar.Low,
                bar.Close,
                (long)bar.Volume
            );
            
            channel.Writer.TryWrite(tradeFlexBar);
        };

        await _client.SubscribeAsync(subscription, cancellationToken);
        Console.WriteLine($"[AlpacaDataFeed] Subscribed to minute bars for {_symbol}");

        // Yield bars as they arrive
        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (channel.Reader.TryRead(out var bar))
            {
                yield return bar;
            }
        }
        
        // Cleanup on exit
        await _client.UnsubscribeAsync(subscription, cancellationToken);
        await _client.DisconnectAsync(cancellationToken);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
