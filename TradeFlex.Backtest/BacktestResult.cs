using TradeFlex.Abstractions;

namespace TradeFlex.Backtest;

/// <summary>
/// Contains the results and performance metrics from a backtest run.
/// </summary>
public sealed class BacktestResult
{
    /// <summary>
    /// Gets the list of executed trades.
    /// </summary>
    public IReadOnlyList<Trade> Trades { get; }

    /// <summary>
    /// Gets the initial cash balance at the start of the backtest.
    /// </summary>
    public decimal InitialCash { get; }

    /// <summary>
    /// Gets the final cash balance at the end of the backtest (excluding open positions).
    /// </summary>
    public decimal FinalCash { get; }

    /// <summary>
    /// Gets the final equity (cash + position value) at the end of the backtest.
    /// </summary>
    public decimal FinalEquity { get; }

    /// <summary>
    /// Gets the total return as a percentage: (FinalEquity - InitialCash) / InitialCash * 100.
    /// </summary>
    public decimal TotalReturnPercent { get; }

    /// <summary>
    /// Gets the win rate as a percentage: winning trades / total round-trip trades * 100.
    /// </summary>
    public decimal WinRatePercent { get; }

    /// <summary>
    /// Gets the maximum drawdown as a percentage (largest peak-to-trough decline).
    /// </summary>
    public decimal MaxDrawdownPercent { get; }

    /// <summary>
    /// Gets the total number of trades executed.
    /// </summary>
    public int TotalTrades { get; }

    /// <summary>
    /// Gets the profit factor: gross profit / gross loss. Returns null if there are no losses.
    /// </summary>
    public decimal? ProfitFactor { get; }

    /// <summary>
    /// Gets the buy-and-hold return as a percentage (benchmark comparison).
    /// </summary>
    public decimal BuyAndHoldReturnPercent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BacktestResult"/> class.
    /// </summary>
    /// <param name="trades">The list of executed trades.</param>
    /// <param name="initialCash">The initial cash balance.</param>
    /// <param name="finalCash">The final cash balance.</param>
    /// <param name="equityCurve">The equity values at each point for drawdown calculation.</param>
    /// <param name="firstPrice">The price at the start of the backtest period.</param>
    /// <param name="lastPrice">The price at the end of the backtest period.</param>
    public BacktestResult(IReadOnlyList<Trade> trades, decimal initialCash, decimal finalCash, IReadOnlyList<decimal> equityCurve, decimal firstPrice = 0, decimal lastPrice = 0)
    {
        Trades = trades;
        InitialCash = initialCash;
        FinalCash = finalCash;
        FinalEquity = equityCurve.Count > 0 ? equityCurve[^1] : finalCash;
        TotalTrades = trades.Count;

        // Calculate Total Return % based on final equity (includes open positions)
        TotalReturnPercent = initialCash != 0
            ? (FinalEquity - initialCash) / initialCash * 100m
            : 0m;

        // Calculate Max Drawdown %
        MaxDrawdownPercent = CalculateMaxDrawdown(equityCurve);

        // Calculate Win Rate and Profit Factor from round-trip trades
        var (winRate, profitFactor) = CalculateTradeMetrics(trades);
        WinRatePercent = winRate;
        ProfitFactor = profitFactor;

        // Calculate Buy-and-Hold return (benchmark)
        BuyAndHoldReturnPercent = firstPrice > 0
            ? (lastPrice - firstPrice) / firstPrice * 100m
            : 0m;
    }

    private static decimal CalculateMaxDrawdown(IReadOnlyList<decimal> equityCurve)
    {
        if (equityCurve.Count == 0)
        {
            return 0m;
        }

        decimal maxDrawdown = 0m;
        decimal peak = equityCurve[0];

        foreach (var equity in equityCurve)
        {
            if (equity > peak)
            {
                peak = equity;
            }

            if (peak > 0)
            {
                var drawdown = (peak - equity) / peak * 100m;
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        return maxDrawdown;
    }

    private static (decimal WinRatePercent, decimal? ProfitFactor) CalculateTradeMetrics(IReadOnlyList<Trade> trades)
    {
        if (trades.Count == 0)
        {
            return (0m, null);
        }

        // Match buy/sell pairs to calculate P&L per round-trip trade
        // This assumes trades are in chronological order and pairs are matched FIFO
        var buyQueue = new Queue<Trade>();
        var roundTripPnLs = new List<decimal>();

        foreach (var trade in trades)
        {
            if (trade.Side == OrderSide.Buy)
            {
                buyQueue.Enqueue(trade);
            }
            else if (trade.Side == OrderSide.Sell && buyQueue.Count > 0)
            {
                var buyTrade = buyQueue.Dequeue();
                // P&L = (sell price - buy price) * quantity
                var pnl = (trade.Price - buyTrade.Price) * Math.Min(trade.Quantity, buyTrade.Quantity);
                roundTripPnLs.Add(pnl);
            }
        }

        if (roundTripPnLs.Count == 0)
        {
            return (0m, null);
        }

        var winningTrades = roundTripPnLs.Count(pnl => pnl > 0);
        var winRate = (decimal)winningTrades / roundTripPnLs.Count * 100m;

        var grossProfit = roundTripPnLs.Where(pnl => pnl > 0).Sum();
        var grossLoss = Math.Abs(roundTripPnLs.Where(pnl => pnl < 0).Sum());

        decimal? profitFactor = grossLoss > 0 ? grossProfit / grossLoss : null;

        return (winRate, profitFactor);
    }
}
