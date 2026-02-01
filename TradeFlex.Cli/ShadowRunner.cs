using System;
using System.Threading;
using System.Threading.Tasks;
using TradeFlex.Abstractions;
using TradeFlex.BrokerAdapters;
using TradeFlex.Core;

namespace TradeFlex.Cli;

public static class ShadowRunner
{
    public static async Task RunAsync(ITradingAlgorithm algorithm, string symbol, IBroker broker)
    {
        Console.WriteLine($"Starting Shadow Trading for {symbol}...");

        // Use injected broker
        var context = new AlgorithmContext(broker);

        await algorithm.InitializeAsync(context);

        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("Stopping...");
            cancellationTokenSource.Cancel();
            e.Cancel = true;
        };

        try
        {
            // Always use Alpaca data feed for stocks
            var config = AlpacaConfiguration.FromEnvironment();
            var feed = new AlpacaDataFeed(symbol, config);

            using (feed as IDisposable)
            {
                await foreach (var bar in feed.GetFeedAsync(cancellationTokenSource.Token))
                {
                    Console.WriteLine($"[Market] {bar.Timestamp:HH:mm:ss} {bar.Symbol} @ {bar.Close:F2} (Vol: {bar.Volume})");

                    // Update price only if PaperBroker (AlpacaBroker gets prices from API)
                    if (broker is PaperBroker paperBroker)
                    {
                        paperBroker.UpdatePrice(bar.Symbol, bar.Close);
                    }

                    await algorithm.OnBarAsync(bar);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }

        await algorithm.OnExitAsync();
        Console.WriteLine("Shadow Trading Stopped.");
        Console.WriteLine("Final Positions:");
        foreach (var kvp in await broker.GetOpenPositionsAsync())
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine($"Final Cash: {await broker.GetAccountBalanceAsync():C}");
    }

    private class AlgorithmContext : IAlgorithmContext
    {
        public IBroker Broker { get; }
        public AlgorithmContext(IBroker broker) => Broker = broker;
    }
}
