using TradeFlex.Abstractions;
using TradeFlex.Core;

namespace TradeFlex.Backtest;

/// <summary>
/// Drives algorithms using <see cref="ParquetBarDataLoader"/> and a <see cref="SimulationClock"/>.
/// </summary>
public sealed class BacktestEngine
{
    private readonly SimulationClock _clock;

    /// <summary>
    /// Initializes the engine with the specified simulation clock.
    /// </summary>
    /// <param name="clock">Clock used to advance simulated time.</param>
    public BacktestEngine(SimulationClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Runs the algorithm over the provided data file.
    /// </summary>
    /// <param name="algorithm">Algorithm instance.</param>
    /// <param name="dataFile">Parquet file containing minute bars.</param>
    /// <param name="symbol">The symbol to trade.</param>
    /// <param name="from">Optional start time filter.</param>
    /// <param name="to">Optional end time filter.</param>
    /// <param name="verbose">Whether to print trade logs.</param>
    /// <returns>Backtest results including trades and performance metrics.</returns>
    public async Task<BacktestResult> RunAsync(ITradingAlgorithm algorithm, string dataFile, string symbol, DateTime? from = null, DateTime? to = null, bool verbose = true)
    {
        // Setup Paper Broker
        const decimal initialCash = 100000m; // Default $100k starting cash
        var broker = new PaperBroker(initialCash, verbose: verbose);
        var context = new AlgorithmContext(broker);
        var equityCurve = new List<decimal> { initialCash };
        decimal firstPrice = 0;
        decimal lastPrice = 0;

        await algorithm.InitializeAsync(context);

        await foreach (var bar in ParquetBarDataLoader.LoadAsync(dataFile, symbol))
        {
            if (from.HasValue && bar.Timestamp < from.Value)
            {
                continue;
            }
            if (to.HasValue && bar.Timestamp > to.Value)
            {
                break;
            }

            _clock.Advance();
            broker.UpdatePrice(symbol, bar.Close);
            await algorithm.OnBarAsync(bar);

            // Track first and last price for buy-and-hold benchmark
            if (firstPrice == 0)
            {
                firstPrice = bar.Close;
            }
            lastPrice = bar.Close;

            // Track equity curve for drawdown calculation
            // Equity = cash + position value at current market price
            var position = await broker.GetPositionAsync(symbol);
            var positionValue = position * bar.Close;
            var equity = await broker.GetAccountBalanceAsync() + positionValue;
            equityCurve.Add(equity);
        }

        await algorithm.OnExitAsync();

        var finalCash = await broker.GetAccountBalanceAsync();
        return new BacktestResult(broker.Trades.ToList(), initialCash, finalCash, equityCurve, firstPrice, lastPrice);
    }

    private class AlgorithmContext : IAlgorithmContext
    {
        public IBroker Broker { get; }
        public AlgorithmContext(IBroker broker) => Broker = broker;
    }
}
