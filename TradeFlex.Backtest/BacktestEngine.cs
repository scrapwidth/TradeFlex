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
    /// <param name="from">Optional start time filter.</param>
    /// <param name="to">Optional end time filter.</param>
    /// <returns>Trades produced by the algorithm.</returns>
    public async Task<List<Trade>> RunAsync(ITradingAlgorithm algorithm, string dataFile, DateTime? from = null, DateTime? to = null)
    {
        var trades = new List<Trade>();
        algorithm.Initialize();

        await foreach (var bar in ParquetBarDataLoader.LoadAsync(dataFile))
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
            algorithm.OnBar(bar);
        }

        algorithm.OnExit();
        return trades;
    }
}
