using System.CommandLine;
using System.Reflection;
using TradeFlex.Abstractions;
using TradeFlex.Backtest;
using TradeFlex.Core;

var backtest = new Command("backtest", "Run a historical back-test")
{
    new Option<FileInfo>("--algo", "Path to algorithm DLL") { IsRequired = true },
    new Option<string>("--data", "Path to Parquet file") { IsRequired = true },
    new Option<DateTime?>("--from", "Start timestamp (UTC)"),
    new Option<DateTime?>("--to", "End timestamp (UTC)")
};

backtest.SetHandler(async (FileInfo algo, string data, DateTime? from, DateTime? to) =>
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

    var trades = await engine.RunAsync(algorithm, data, from, to);
    Console.WriteLine($"Processed {trades.Count} trades");
},
    backtest.Options[0] as Option<FileInfo>,
    backtest.Options[1] as Option<string>,
    backtest.Options[2] as Option<DateTime?>,
    backtest.Options[3] as Option<DateTime?>);

var root = new RootCommand("tradeflex command line interface");
root.Add(backtest);

return await root.InvokeAsync(args);
