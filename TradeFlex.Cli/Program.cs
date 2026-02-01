using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using TradeFlex.Abstractions;
using TradeFlex.Backtest;
using TradeFlex.Core;
using TradeFlex.Cli;
using TradeFlex.BrokerAdapters;
using TradeFlex.SampleStrategies;

// Common algorithm options
var algoOption = new Option<string>("--algo", "Algorithm name: SMA, RSI, MARTINGALE, ML") { IsRequired = true };
var fastPeriodOption = new Option<int>("--fast-period", () => 5, "SMA fast period (default: 5)");
var slowPeriodOption = new Option<int>("--slow-period", () => 20, "SMA slow period (default: 20)");
var rsiPeriodOption = new Option<int>("--rsi-period", () => 14, "RSI period (default: 14)");
var oversoldOption = new Option<int>("--oversold", () => 30, "RSI oversold threshold (default: 30)");
var overboughtOption = new Option<int>("--overbought", () => 70, "RSI overbought threshold (default: 70)");
// Martingale parameters
var basePosOption = new Option<decimal>("--base-pos", () => 0.05m, "Martingale base position % (default: 5%)");
var takeProfitOption = new Option<decimal>("--take-profit", () => 0.02m, "Martingale take profit % (default: 2%)");
var stopLossOption = new Option<decimal>("--stop-loss", () => 0.01m, "Martingale stop loss % (default: 1%)");
// ML parameters
var warmupBarsOption = new Option<int>("--warmup-bars", () => 300, "ML warmup bars for training (default: 300)");
var predictionHorizonOption = new Option<int>("--prediction-horizon", () => 5, "ML prediction horizon in bars (default: 5)");
var bullishThresholdOption = new Option<float>("--bullish-threshold", () => 0.6f, "ML buy probability threshold (default: 0.6)");
var bearishThresholdOption = new Option<float>("--bearish-threshold", () => 0.4f, "ML sell probability threshold (default: 0.4)");

// Backtest-specific options
var dataOption = new Option<string>("--data", "Path to Parquet file") { IsRequired = true };
var symbolOption = new Option<string>("--symbol", "Symbol to trade") { IsRequired = true };
var fromOption = new Option<DateTime?>("--from", "Start timestamp (UTC)");
var toOption = new Option<DateTime?>("--to", "End timestamp (UTC)");
var verboseOption = new Option<bool>("--verbose", () => true, "Enable verbose trade logging");

var backtest = new Command("backtest", "Run a historical back-test")
{
    algoOption,
    dataOption,
    symbolOption,
    fromOption,
    toOption,
    verboseOption,
    fastPeriodOption,
    slowPeriodOption,
    rsiPeriodOption,
    oversoldOption,
    overboughtOption,
    basePosOption,
    takeProfitOption,
    stopLossOption,
    warmupBarsOption,
    predictionHorizonOption,
    bullishThresholdOption,
    bearishThresholdOption
};

backtest.SetHandler(async (InvocationContext ctx) =>
{
    var algo = ctx.ParseResult.GetValueForOption(algoOption)!;
    var data = ctx.ParseResult.GetValueForOption(dataOption)!;
    var symbol = ctx.ParseResult.GetValueForOption(symbolOption)!;
    var from = ctx.ParseResult.GetValueForOption(fromOption);
    var to = ctx.ParseResult.GetValueForOption(toOption);
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
    var fastPeriod = ctx.ParseResult.GetValueForOption(fastPeriodOption);
    var slowPeriod = ctx.ParseResult.GetValueForOption(slowPeriodOption);
    var rsiPeriod = ctx.ParseResult.GetValueForOption(rsiPeriodOption);
    var oversold = ctx.ParseResult.GetValueForOption(oversoldOption);
    var overbought = ctx.ParseResult.GetValueForOption(overboughtOption);
    var basePos = ctx.ParseResult.GetValueForOption(basePosOption);
    var takeProfit = ctx.ParseResult.GetValueForOption(takeProfitOption);
    var stopLoss = ctx.ParseResult.GetValueForOption(stopLossOption);
    var warmupBars = ctx.ParseResult.GetValueForOption(warmupBarsOption);
    var predictionHorizon = ctx.ParseResult.GetValueForOption(predictionHorizonOption);
    var bullishThreshold = ctx.ParseResult.GetValueForOption(bullishThresholdOption);
    var bearishThreshold = ctx.ParseResult.GetValueForOption(bearishThresholdOption);

    var algorithm = CreateAlgorithm(algo, fastPeriod, slowPeriod, rsiPeriod, oversold, overbought, basePos, takeProfit, stopLoss, warmupBars, predictionHorizon, bullishThreshold, bearishThreshold);
    var start = from ?? DateTime.UtcNow;
    var clock = new SimulationClock(start, TimeSpan.FromMinutes(1));

    // Configure logging based on verbose flag
    ILoggerFactory? loggerFactory = null;
    if (verbose)
    {
        loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    var engine = new BacktestEngine(clock, loggerFactory);

    var result = await engine.RunAsync(algorithm, data, symbol, from, to);

    // Print performance metrics
    Console.WriteLine();
    Console.WriteLine("=== Backtest Results ===");
    Console.WriteLine($"Algorithm:         {algo.ToUpper()}");
    if (algo.ToUpper() == "SMA")
    {
        Console.WriteLine($"Parameters:        Fast={fastPeriod}, Slow={slowPeriod}");
    }
    else if (algo.ToUpper() == "RSI")
    {
        Console.WriteLine($"Parameters:        Period={rsiPeriod}, Oversold={oversold}, Overbought={overbought}");
    }
    else if (algo.ToUpper() == "MARTINGALE")
    {
        Console.WriteLine($"Parameters:        Base={basePos:P0}, TP={takeProfit:P0}, SL={stopLoss:P0}");
    }
    else if (algo.ToUpper() == "ML")
    {
        Console.WriteLine($"Parameters:        Warmup={warmupBars}, Horizon={predictionHorizon}, Bull={bullishThreshold}, Bear={bearishThreshold}");
    }
    Console.WriteLine($"Initial Cash:      ${result.InitialCash:N2}");
    Console.WriteLine($"Final Equity:      ${result.FinalEquity:N2}");
    Console.WriteLine($"Final Cash:        ${result.FinalCash:N2}");
    Console.WriteLine($"Total Return:      {result.TotalReturnPercent:N2}%");
    Console.WriteLine($"Buy & Hold:        {result.BuyAndHoldReturnPercent:N2}%");
    Console.WriteLine($"Max Drawdown:      {result.MaxDrawdownPercent:N2}%");
    Console.WriteLine($"Total Trades:      {result.TotalTrades}");
    Console.WriteLine($"Win Rate:          {result.WinRatePercent:N2}%");
    Console.WriteLine($"Profit Factor:     {(result.ProfitFactor.HasValue ? result.ProfitFactor.Value.ToString("N2") : "N/A")}");
    Console.WriteLine("========================");
});

// Shadow-specific options
var shadowSymbolOption = new Option<string>("--symbol", "Symbol to trade") { IsRequired = true };
var brokerOption = new Option<string>("--broker", () => "paper", "Broker: 'paper' or 'alpaca'");

var shadow = new Command("shadow", "Run a shadow trading session")
{
    algoOption,
    shadowSymbolOption,
    brokerOption,
    fastPeriodOption,
    slowPeriodOption,
    rsiPeriodOption,
    oversoldOption,
    overboughtOption,
    basePosOption,
    takeProfitOption,
    stopLossOption,
    warmupBarsOption,
    predictionHorizonOption,
    bullishThresholdOption,
    bearishThresholdOption
};

shadow.SetHandler(async (InvocationContext ctx) =>
{
    var algo = ctx.ParseResult.GetValueForOption(algoOption)!;
    var symbol = ctx.ParseResult.GetValueForOption(shadowSymbolOption)!;
    var brokerType = ctx.ParseResult.GetValueForOption(brokerOption)!;
    var fastPeriod = ctx.ParseResult.GetValueForOption(fastPeriodOption);
    var slowPeriod = ctx.ParseResult.GetValueForOption(slowPeriodOption);
    var rsiPeriod = ctx.ParseResult.GetValueForOption(rsiPeriodOption);
    var oversold = ctx.ParseResult.GetValueForOption(oversoldOption);
    var overbought = ctx.ParseResult.GetValueForOption(overboughtOption);
    var basePos = ctx.ParseResult.GetValueForOption(basePosOption);
    var takeProfit = ctx.ParseResult.GetValueForOption(takeProfitOption);
    var stopLoss = ctx.ParseResult.GetValueForOption(stopLossOption);
    var warmupBars = ctx.ParseResult.GetValueForOption(warmupBarsOption);
    var predictionHorizon = ctx.ParseResult.GetValueForOption(predictionHorizonOption);
    var bullishThreshold = ctx.ParseResult.GetValueForOption(bullishThresholdOption);
    var bearishThreshold = ctx.ParseResult.GetValueForOption(bearishThresholdOption);

    var algorithm = CreateAlgorithm(algo, fastPeriod, slowPeriod, rsiPeriod, oversold, overbought, basePos, takeProfit, stopLoss, warmupBars, predictionHorizon, bullishThreshold, bearishThreshold);

    // Create broker based on selection
    IBroker broker = brokerType.ToLower() switch
    {
        "alpaca" => await CreateAlpacaBrokerAsync(),
        "paper" => new PaperBroker(100000m),
        _ => throw new ArgumentException($"Unknown broker type: {brokerType}. Use 'paper' or 'alpaca'.")
    };

    await ShadowRunner.RunAsync(algorithm, symbol, broker);
});

static ITradingAlgorithm CreateAlgorithm(string name, int fastPeriod, int slowPeriod, int rsiPeriod, int oversold, int overbought, decimal basePos, decimal takeProfit, decimal stopLoss, int warmupBars, int predictionHorizon, float bullishThreshold, float bearishThreshold)
{
    return name.ToUpper() switch
    {
        "SMA" => new SimpleSmaCrossoverAlgorithm(fastPeriod, slowPeriod),
        "RSI" => new RsiMeanReversionAlgorithm(rsiPeriod, oversold, overbought),
        "MARTINGALE" => new MartingaleAlgorithm(5, basePos, takeProfit, stopLoss),
        "ML" => new MlPredictorAlgorithm(warmupBars, predictionHorizon, bullishThreshold, bearishThreshold),
        _ => throw new ArgumentException($"Unknown algorithm: {name}. Available: SMA, RSI, MARTINGALE, ML")
    };
}

static async Task<IBroker> CreateAlpacaBrokerAsync()
{
    try
    {
        var config = AlpacaConfiguration.FromEnvironment();
        return await AlpacaBroker.CreateAsync(config);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to create Alpaca broker: {ex.Message}");
        Console.Error.WriteLine("Make sure to set environment variables:");
        Console.Error.WriteLine("  ALPACA_API_KEY_ID");
        Console.Error.WriteLine("  ALPACA_SECRET_KEY");
        Console.Error.WriteLine("  ALPACA_USE_PAPER (optional, defaults to true)");
        throw;
    }
}

// Download options
var downloadSymbolOption = new Option<string>("--symbol", "Symbol to download (e.g., AAPL, SPY)") { IsRequired = true };
var downloadFromOption = new Option<DateTime>("--from", "Start date") { IsRequired = true };
var downloadToOption = new Option<DateTime>("--to", "End date") { IsRequired = true };
var granularityOption = new Option<string>("--granularity", () => "1d", "Candle size: 1m, 5m, 15m, 1h, 1d");
var outputOption = new Option<string>("--output", "Output filename") { IsRequired = true };

var download = new Command("download", "Download historical market data from Alpaca")
{
    downloadSymbolOption,
    downloadFromOption,
    downloadToOption,
    granularityOption,
    outputOption
};

download.SetHandler(async (string symbol, DateTime from, DateTime to, string granularity, string output) =>
{
    await DataDownloader.DownloadAsync(symbol, from, to, granularity, output);
},
    downloadSymbolOption,
    downloadFromOption,
    downloadToOption,
    granularityOption,
    outputOption);

// Optimize command
var optimizeAlgoOption = new Option<string>("--algo", () => "ALL", "Algorithm to optimize: SMA, RSI, MARTINGALE, or ALL");
var optimizeDataOption = new Option<string>("--data", "Path to Parquet file") { IsRequired = true };
var optimizeSymbolOption = new Option<string>("--symbol", "Symbol to trade") { IsRequired = true };
var topNOption = new Option<int>("--top", () => 10, "Number of top results to show");

var optimize = new Command("optimize", "Find optimal algorithm parameters for a dataset (WARNING: overfitting!)")
{
    optimizeAlgoOption,
    optimizeDataOption,
    optimizeSymbolOption,
    topNOption
};

optimize.SetHandler(async (string algo, string data, string symbol, int topN) =>
{
    Console.WriteLine($"Optimizing {algo} parameters on {symbol}...");
    Console.WriteLine("This may take a while...");
    Console.WriteLine();

    var results = await Optimizer.OptimizeAsync(
        data,
        symbol,
        algo,
        topN,
        progress => Console.Write($"\r{progress.PadRight(60)}")
    );

    Console.WriteLine();

    if (results.Count > 0)
    {
        Optimizer.PrintResults(results, results[0].BuyAndHoldReturn);
    }
    else
    {
        Console.WriteLine("No results found.");
    }
},
    optimizeAlgoOption,
    optimizeDataOption,
    optimizeSymbolOption,
    topNOption);

var root = new RootCommand("tradeflex command line interface");
root.Add(backtest);
root.Add(shadow);
root.Add(download);
root.Add(optimize);

return await root.InvokeAsync(args);
