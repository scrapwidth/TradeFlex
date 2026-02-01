using System.CommandLine;
using System.Reflection;
using TradeFlex.Abstractions;
using TradeFlex.Backtest;
using TradeFlex.Core;
using TradeFlex.Cli;
using TradeFlex.BrokerAdapters;

var backtest = new Command("backtest", "Run a historical back-test")
{
    new Option<FileInfo>("--algo", "Path to algorithm DLL") { IsRequired = true },
    new Option<string>("--data", "Path to Parquet file") { IsRequired = true },
    new Option<string>("--symbol", "Symbol to trade") { IsRequired = true },
    new Option<DateTime?>("--from", "Start timestamp (UTC)"),
    new Option<DateTime?>("--to", "End timestamp (UTC)")
};

backtest.SetHandler(async (FileInfo algo, string data, string symbol, DateTime? from, DateTime? to) =>
{
    var asm = Assembly.LoadFrom(algo.FullName);
    var algoType = asm.GetTypes().FirstOrDefault(t => typeof(ITradingAlgorithm).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
    if (algoType == null)
    {
        Console.Error.WriteLine("No algorithm found in assembly");
        return;
    }

    var algorithm = AlgorithmRunner.CreateAlgorithm(algoType);
    var start = from ?? DateTime.UtcNow;
    var clock = new SimulationClock(start, TimeSpan.FromMinutes(1));
    var engine = new BacktestEngine(clock);

    var result = await engine.RunAsync(algorithm, data, symbol, from, to);

    // Print performance metrics
    Console.WriteLine();
    Console.WriteLine("=== Backtest Results ===");
    Console.WriteLine($"Initial Cash:      ${result.InitialCash:N2}");
    Console.WriteLine($"Final Cash:        ${result.FinalCash:N2}");
    Console.WriteLine($"Total Return:      {result.TotalReturnPercent:N2}%");
    Console.WriteLine($"Max Drawdown:      {result.MaxDrawdownPercent:N2}%");
    Console.WriteLine($"Total Trades:      {result.TotalTrades}");
    Console.WriteLine($"Win Rate:          {result.WinRatePercent:N2}%");
    Console.WriteLine($"Profit Factor:     {(result.ProfitFactor.HasValue ? result.ProfitFactor.Value.ToString("N2") : "N/A")}");
    Console.WriteLine("========================");
},
    backtest.Options[0] as Option<FileInfo>,
    backtest.Options[1] as Option<string>,
    backtest.Options[2] as Option<string>,
    backtest.Options[3] as Option<DateTime?>,
    backtest.Options[4] as Option<DateTime?>);

var shadow = new Command("shadow", "Run a shadow trading session")
{
    new Option<FileInfo>("--algo", "Path to algorithm DLL") { IsRequired = true },
    new Option<string>("--symbol", "Symbol to trade") { IsRequired = true },
    new Option<string>("--broker", () => "paper", "Broker: 'paper' or 'alpaca'")
};

shadow.SetHandler(async (FileInfo algo, string symbol, string brokerType) =>
{
    // Create broker based on selection
    IBroker broker = brokerType.ToLower() switch
    {
        "alpaca" => CreateAlpacaBroker(),
        "paper" => new PaperBroker(100000m),
        _ => throw new ArgumentException($"Unknown broker type: {brokerType}. Use 'paper' or 'alpaca'.")
    };

    await ShadowRunner.RunAsync(algo, symbol, broker);
},
    shadow.Options[0] as Option<FileInfo>,
    shadow.Options[1] as Option<string>,
    shadow.Options[2] as Option<string>);

static IBroker CreateAlpacaBroker()
{
    try
    {
        var config = AlpacaConfiguration.FromEnvironment();
        return new AlpacaBroker(config);
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

var download = new Command("download", "Download historical market data from Alpaca")
{
    new Option<string>("--symbol", "Symbol to download (e.g., AAPL, SPY)") { IsRequired = true },
    new Option<DateTime>("--from", "Start date") { IsRequired = true },
    new Option<DateTime>("--to", "End date") { IsRequired = true },
    new Option<string>("--granularity", () => "1d", "Candle size: 1m, 5m, 15m, 1h, 1d"),
    new Option<string>("--output", "Output filename") { IsRequired = true }
};

download.SetHandler(async (string symbol, DateTime from, DateTime to, string granularity, string output) =>
{
    await DataDownloader.DownloadAsync(symbol, from, to, granularity, output);
},
    download.Options[0] as Option<string>,
    download.Options[1] as Option<DateTime>,
    download.Options[2] as Option<DateTime>,
    download.Options[3] as Option<string>,
    download.Options[4] as Option<string>);

var root = new RootCommand("tradeflex command line interface");
root.Add(backtest);
root.Add(shadow);
root.Add(download);

return await root.InvokeAsync(args);
