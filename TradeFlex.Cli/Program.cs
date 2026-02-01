using System.CommandLine;
using System.CommandLine.Invocation;
using TradeFlex.Abstractions;
using TradeFlex.Backtest;
using TradeFlex.Core;
using TradeFlex.Cli;
using TradeFlex.BrokerAdapters;
using TradeFlex.SampleStrategies;

// Common algorithm options
var algoOption = new Option<string>("--algo", "Algorithm name: SMA, RSI") { IsRequired = true };
var fastPeriodOption = new Option<int>("--fast-period", () => 5, "SMA fast period (default: 5)");
var slowPeriodOption = new Option<int>("--slow-period", () => 20, "SMA slow period (default: 20)");
var rsiPeriodOption = new Option<int>("--rsi-period", () => 14, "RSI period (default: 14)");
var oversoldOption = new Option<int>("--oversold", () => 30, "RSI oversold threshold (default: 30)");
var overboughtOption = new Option<int>("--overbought", () => 70, "RSI overbought threshold (default: 70)");

// Backtest-specific options
var dataOption = new Option<string>("--data", "Path to Parquet file") { IsRequired = true };
var symbolOption = new Option<string>("--symbol", "Symbol to trade") { IsRequired = true };
var fromOption = new Option<DateTime?>("--from", "Start timestamp (UTC)");
var toOption = new Option<DateTime?>("--to", "End timestamp (UTC)");

var backtest = new Command("backtest", "Run a historical back-test")
{
    algoOption,
    dataOption,
    symbolOption,
    fromOption,
    toOption,
    fastPeriodOption,
    slowPeriodOption,
    rsiPeriodOption,
    oversoldOption,
    overboughtOption
};

backtest.SetHandler(async (InvocationContext ctx) =>
{
    var algo = ctx.ParseResult.GetValueForOption(algoOption)!;
    var data = ctx.ParseResult.GetValueForOption(dataOption)!;
    var symbol = ctx.ParseResult.GetValueForOption(symbolOption)!;
    var from = ctx.ParseResult.GetValueForOption(fromOption);
    var to = ctx.ParseResult.GetValueForOption(toOption);
    var fastPeriod = ctx.ParseResult.GetValueForOption(fastPeriodOption);
    var slowPeriod = ctx.ParseResult.GetValueForOption(slowPeriodOption);
    var rsiPeriod = ctx.ParseResult.GetValueForOption(rsiPeriodOption);
    var oversold = ctx.ParseResult.GetValueForOption(oversoldOption);
    var overbought = ctx.ParseResult.GetValueForOption(overboughtOption);

    var algorithm = CreateAlgorithm(algo, fastPeriod, slowPeriod, rsiPeriod, oversold, overbought);
    var start = from ?? DateTime.UtcNow;
    var clock = new SimulationClock(start, TimeSpan.FromMinutes(1));
    var engine = new BacktestEngine(clock);

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
    overboughtOption
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

    var algorithm = CreateAlgorithm(algo, fastPeriod, slowPeriod, rsiPeriod, oversold, overbought);

    // Create broker based on selection
    IBroker broker = brokerType.ToLower() switch
    {
        "alpaca" => await CreateAlpacaBrokerAsync(),
        "paper" => new PaperBroker(100000m),
        _ => throw new ArgumentException($"Unknown broker type: {brokerType}. Use 'paper' or 'alpaca'.")
    };

    await ShadowRunner.RunAsync(algorithm, symbol, broker);
});

static ITradingAlgorithm CreateAlgorithm(string name, int fastPeriod, int slowPeriod, int rsiPeriod, int oversold, int overbought)
{
    return name.ToUpper() switch
    {
        "SMA" => new SimpleSmaCrossoverAlgorithm(fastPeriod, slowPeriod),
        "RSI" => new RsiMeanReversionAlgorithm(rsiPeriod, oversold, overbought),
        _ => throw new ArgumentException($"Unknown algorithm: {name}. Available: SMA, RSI")
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

var root = new RootCommand("tradeflex command line interface");
root.Add(backtest);
root.Add(shadow);
root.Add(download);

return await root.InvokeAsync(args);
