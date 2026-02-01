using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradeFlex.Abstractions;
using TradeFlex.Backtest;
using TradeFlex.Core;
using TradeFlex.SampleStrategies;

namespace TradeFlex.Cli;

/// <summary>
/// Parameter optimizer that searches for best-performing algorithm configurations.
/// WARNING: Optimized parameters are overfitted to historical data and may not work in the future.
/// </summary>
public static class Optimizer
{
    public record OptimizationResult(
        string Algorithm,
        Dictionary<string, object> Parameters,
        decimal TotalReturn,
        decimal BuyAndHoldReturn,
        decimal MaxDrawdown,
        int TotalTrades,
        decimal WinRate,
        decimal? ProfitFactor
    );

    public static async Task<List<OptimizationResult>> OptimizeAsync(
        string dataFile,
        string symbol,
        string algorithm,
        int topN = 10,
        Action<string>? progressCallback = null)
    {
        var results = new List<OptimizationResult>();

        switch (algorithm.ToUpper())
        {
            case "SMA":
                results = await OptimizeSmaAsync(dataFile, symbol, progressCallback);
                break;
            case "RSI":
                results = await OptimizeRsiAsync(dataFile, symbol, progressCallback);
                break;
            case "MARTINGALE":
                results = await OptimizeMartingaleAsync(dataFile, symbol, progressCallback);
                break;
            case "ALL":
                var smaResults = await OptimizeSmaAsync(dataFile, symbol, progressCallback);
                var rsiResults = await OptimizeRsiAsync(dataFile, symbol, progressCallback);
                var martingaleResults = await OptimizeMartingaleAsync(dataFile, symbol, progressCallback);
                results = smaResults.Concat(rsiResults).Concat(martingaleResults).ToList();
                break;
            default:
                throw new ArgumentException($"Unknown algorithm: {algorithm}");
        }

        // Sort by total return descending and take top N
        return results
            .OrderByDescending(r => r.TotalReturn)
            .Take(topN)
            .ToList();
    }

    private static async Task<List<OptimizationResult>> OptimizeSmaAsync(
        string dataFile, string symbol, Action<string>? progressCallback)
    {
        var results = new List<OptimizationResult>();
        var fastPeriods = new[] { 3, 5, 7, 10, 15 };
        var slowPeriods = new[] { 10, 15, 20, 30, 50, 100 };

        int total = fastPeriods.Length * slowPeriods.Length;
        int current = 0;

        foreach (var fast in fastPeriods)
        {
            foreach (var slow in slowPeriods)
            {
                if (fast >= slow) continue; // Fast must be less than slow

                current++;
                progressCallback?.Invoke($"SMA: Testing {fast}/{slow} ({current}/{total})");

                var algo = new SimpleSmaCrossoverAlgorithm(fast, slow);
                var result = await RunBacktestAsync(algo, dataFile, symbol);

                results.Add(new OptimizationResult(
                    "SMA",
                    new Dictionary<string, object> { ["FastPeriod"] = fast, ["SlowPeriod"] = slow },
                    result.TotalReturnPercent,
                    result.BuyAndHoldReturnPercent,
                    result.MaxDrawdownPercent,
                    result.TotalTrades,
                    result.WinRatePercent,
                    result.ProfitFactor
                ));
            }
        }

        return results;
    }

    private static async Task<List<OptimizationResult>> OptimizeRsiAsync(
        string dataFile, string symbol, Action<string>? progressCallback)
    {
        var results = new List<OptimizationResult>();
        var periods = new[] { 7, 10, 14, 21, 28 };
        var oversoldLevels = new[] { 20, 25, 30, 35, 40, 45 };
        var overboughtLevels = new[] { 55, 60, 65, 70, 75, 80, 85, 90 };

        int total = periods.Length * oversoldLevels.Length * overboughtLevels.Length;
        int current = 0;

        foreach (var period in periods)
        {
            foreach (var oversold in oversoldLevels)
            {
                foreach (var overbought in overboughtLevels)
                {
                    if (oversold >= overbought) continue; // Oversold must be less than overbought

                    current++;
                    progressCallback?.Invoke($"RSI: Testing {period}/{oversold}/{overbought} ({current}/{total})");

                    var algo = new RsiMeanReversionAlgorithm(period, oversold, overbought);
                    var result = await RunBacktestAsync(algo, dataFile, symbol);

                    results.Add(new OptimizationResult(
                        "RSI",
                        new Dictionary<string, object>
                        {
                            ["Period"] = period,
                            ["Oversold"] = oversold,
                            ["Overbought"] = overbought
                        },
                        result.TotalReturnPercent,
                        result.BuyAndHoldReturnPercent,
                        result.MaxDrawdownPercent,
                        result.TotalTrades,
                        result.WinRatePercent,
                        result.ProfitFactor
                    ));
                }
            }
        }

        return results;
    }

    private static async Task<List<OptimizationResult>> OptimizeMartingaleAsync(
        string dataFile, string symbol, Action<string>? progressCallback)
    {
        var results = new List<OptimizationResult>();
        var basePositions = new[] { 0.05m, 0.10m, 0.15m, 0.20m };
        var takeProfits = new[] { 0.02m, 0.03m, 0.05m, 0.08m, 0.10m };
        var stopLosses = new[] { 0.01m, 0.02m, 0.03m, 0.05m };

        int total = basePositions.Length * takeProfits.Length * stopLosses.Length;
        int current = 0;

        foreach (var basePos in basePositions)
        {
            foreach (var tp in takeProfits)
            {
                foreach (var sl in stopLosses)
                {
                    current++;
                    progressCallback?.Invoke($"MARTINGALE: Testing {basePos:P0}/{tp:P0}/{sl:P0} ({current}/{total})");

                    var algo = new MartingaleAlgorithm(5, basePos, tp, sl);
                    var result = await RunBacktestAsync(algo, dataFile, symbol);

                    results.Add(new OptimizationResult(
                        "MARTINGALE",
                        new Dictionary<string, object>
                        {
                            ["BasePosition"] = basePos,
                            ["TakeProfit"] = tp,
                            ["StopLoss"] = sl
                        },
                        result.TotalReturnPercent,
                        result.BuyAndHoldReturnPercent,
                        result.MaxDrawdownPercent,
                        result.TotalTrades,
                        result.WinRatePercent,
                        result.ProfitFactor
                    ));
                }
            }
        }

        return results;
    }

    private static async Task<BacktestResult> RunBacktestAsync(ITradingAlgorithm algorithm, string dataFile, string symbol)
    {
        var clock = new SimulationClock(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        var engine = new BacktestEngine(clock);
        return await engine.RunAsync(algorithm, dataFile, symbol, verbose: false);
    }

    public static void PrintResults(List<OptimizationResult> results, decimal buyAndHoldReturn)
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                         OPTIMIZATION RESULTS                                  ║");
        Console.WriteLine("║  WARNING: These parameters are overfitted to historical data!                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Buy & Hold Benchmark: {buyAndHoldReturn:N2}%");
        Console.WriteLine();
        Console.WriteLine("┌────┬────────────┬─────────────────────────────────────┬──────────┬──────────┬────────┬─────────┐");
        Console.WriteLine("│ #  │ Algorithm  │ Parameters                          │ Return   │ Drawdown │ Trades │ Win %   │");
        Console.WriteLine("├────┼────────────┼─────────────────────────────────────┼──────────┼──────────┼────────┼─────────┤");

        int rank = 1;
        foreach (var result in results)
        {
            var paramsStr = FormatParameters(result.Algorithm, result.Parameters);
            var beatsBenchmark = result.TotalReturn > buyAndHoldReturn ? "✓" : " ";

            Console.WriteLine($"│ {rank,2} │ {result.Algorithm,-10} │ {paramsStr,-35} │ {result.TotalReturn,6:N2}% {beatsBenchmark}│ {result.MaxDrawdown,6:N2}%  │ {result.TotalTrades,6} │ {result.WinRate,5:N1}%  │");
            rank++;
        }

        Console.WriteLine("└────┴────────────┴─────────────────────────────────────┴──────────┴──────────┴────────┴─────────┘");
        Console.WriteLine();

        // Print the best result details
        if (results.Count > 0)
        {
            var best = results[0];
            Console.WriteLine("═══ BEST CONFIGURATION ═══");
            Console.WriteLine($"Algorithm: {best.Algorithm}");
            Console.WriteLine($"Parameters:");
            foreach (var param in best.Parameters)
            {
                if (param.Value is decimal d)
                    Console.WriteLine($"  --{ParamToCliFlag(param.Key)}: {d:P0}");
                else
                    Console.WriteLine($"  --{ParamToCliFlag(param.Key)}: {param.Value}");
            }
            Console.WriteLine();
            Console.WriteLine($"Total Return:  {best.TotalReturn:N2}%");
            Console.WriteLine($"Buy & Hold:    {best.BuyAndHoldReturn:N2}%");
            Console.WriteLine($"Outperformance: {best.TotalReturn - best.BuyAndHoldReturn:N2}%");
            Console.WriteLine($"Max Drawdown:  {best.MaxDrawdown:N2}%");
            Console.WriteLine($"Total Trades:  {best.TotalTrades}");
            Console.WriteLine($"Win Rate:      {best.WinRate:N2}%");
            Console.WriteLine($"Profit Factor: {(best.ProfitFactor.HasValue ? best.ProfitFactor.Value.ToString("N2") : "N/A")}");
            Console.WriteLine();

            // Generate CLI command
            Console.WriteLine("═══ CLI COMMAND ═══");
            Console.Write($"dotnet run --project TradeFlex.Cli -- backtest --algo {best.Algorithm} --data <file> --symbol <SYMBOL>");
            foreach (var param in best.Parameters)
            {
                if (param.Value is decimal d)
                    Console.Write($" --{ParamToCliFlag(param.Key)} {d}");
                else
                    Console.Write($" --{ParamToCliFlag(param.Key)} {param.Value}");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("⚠️  IMPORTANT: Past performance does not guarantee future results.");
        Console.WriteLine("   These optimized parameters are likely overfitted to this specific dataset.");
        Console.WriteLine("   Always test on out-of-sample data before using in live trading.");
    }

    private static string FormatParameters(string algorithm, Dictionary<string, object> parameters)
    {
        return algorithm switch
        {
            "SMA" => $"Fast={parameters["FastPeriod"]}, Slow={parameters["SlowPeriod"]}",
            "RSI" => $"P={parameters["Period"]}, OS={parameters["Oversold"]}, OB={parameters["Overbought"]}",
            "MARTINGALE" => $"Base={(decimal)parameters["BasePosition"]:P0}, TP={(decimal)parameters["TakeProfit"]:P0}, SL={(decimal)parameters["StopLoss"]:P0}",
            _ => string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))
        };
    }

    private static string ParamToCliFlag(string paramName)
    {
        return paramName switch
        {
            "FastPeriod" => "fast-period",
            "SlowPeriod" => "slow-period",
            "Period" => "rsi-period",
            "Oversold" => "oversold",
            "Overbought" => "overbought",
            "BasePosition" => "base-pos",
            "TakeProfit" => "take-profit",
            "StopLoss" => "stop-loss",
            _ => paramName.ToLower()
        };
    }
}
