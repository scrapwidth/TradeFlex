using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TradeFlex.Abstractions;
using TradeFlex.Core;

namespace TradeFlex.Cli;

public static class ShadowRunner
{
    public static async Task RunAsync(FileInfo algoFile, string symbol, IBroker broker)
    {
        Console.WriteLine($"Starting Shadow Trading for {symbol}...");
        
        // Load Algorithm
        var asm = Assembly.LoadFrom(algoFile.FullName);
        var algoType = asm.GetTypes().FirstOrDefault(t => typeof(ITradingAlgorithm).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
        if (algoType == null)
        {
            Console.Error.WriteLine("No algorithm found in assembly");
            return;
        }

        var algorithm = (ITradingAlgorithm)Activator.CreateInstance(algoType)!;

        // Use injected broker
        var context = new AlgorithmContext(broker);
        
        algorithm.Initialize(context);

        // Simulate Live Feed
        // In a real scenario, this would connect to a websocket or API.
        // Here we just generate random bars for demonstration.
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("Stopping...");
            cancellationTokenSource.Cancel();
            e.Cancel = true;
        };

        try
        {
            // Normalize symbol for Coinbase (e.g. BTCUSD -> BTC-USD)
            // Simple heuristic: if no dash and ends with USD, insert dash.
            var coinbaseSymbol = symbol;
            if (!symbol.Contains("-") && symbol.EndsWith("USD"))
            {
                coinbaseSymbol = symbol.Insert(symbol.Length - 3, "-");
            }

            var feed = new CoinbaseDataFeed(coinbaseSymbol);
            
            await foreach (var bar in feed.GetFeedAsync(cancellationTokenSource.Token))
            {
                Console.WriteLine($"[Market] {bar.Timestamp:HH:mm:ss} {bar.Symbol} @ {bar.Close:F2} (Vol: {bar.Volume})");
                
                // Update price only if PaperBroker (AlpacaBroker gets prices from API)
                if (broker is PaperBroker paperBroker)
                {
                    paperBroker.UpdatePrice(bar.Symbol, bar.Close);
                }
                
                algorithm.OnBar(bar);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }

        algorithm.OnExit();
        Console.WriteLine("Shadow Trading Stopped.");
        Console.WriteLine("Final Positions:");
        foreach (var kvp in broker.GetOpenPositions())
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine($"Final Cash: {broker.GetAccountBalance():C}");
    }

    private class AlgorithmContext : IAlgorithmContext
    {
        public IBroker Broker { get; }
        public AlgorithmContext(IBroker broker) => Broker = broker;
    }
}
