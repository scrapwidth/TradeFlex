using TradeFlex.Abstractions;
using TradeFlex.Backtest;

namespace TradeFlex.Tests;

public class BacktestResultTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 12000m, equityCurve);

        Assert.Equal(10000m, result.InitialCash);
        Assert.Equal(12000m, result.FinalCash);
        Assert.Empty(result.Trades);
        Assert.Equal(0, result.TotalTrades);
    }

    [Fact]
    public void TotalReturnPercent_CalculatedCorrectly()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal> { 10000m, 12000m };

        var result = new BacktestResult(trades, 10000m, 12000m, equityCurve);

        // (12000 - 10000) / 10000 * 100 = 20%
        Assert.Equal(20m, result.TotalReturnPercent);
    }

    [Fact]
    public void TotalReturnPercent_NegativeReturn()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal> { 10000m, 8000m };

        var result = new BacktestResult(trades, 10000m, 8000m, equityCurve);

        // (8000 - 10000) / 10000 * 100 = -20%
        Assert.Equal(-20m, result.TotalReturnPercent);
    }

    [Fact]
    public void TotalReturnPercent_ZeroInitialCash_ReturnsZero()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal> { 0m };

        var result = new BacktestResult(trades, 0m, 100m, equityCurve);

        Assert.Equal(0m, result.TotalReturnPercent);
    }

    [Fact]
    public void MaxDrawdownPercent_EmptyEquityCurve_ReturnsZero()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal>();

        var result = new BacktestResult(trades, 10000m, 10000m, equityCurve);

        Assert.Equal(0m, result.MaxDrawdownPercent);
    }

    [Fact]
    public void MaxDrawdownPercent_NoDrawdown_ReturnsZero()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal> { 10000m, 11000m, 12000m, 13000m };

        var result = new BacktestResult(trades, 10000m, 13000m, equityCurve);

        Assert.Equal(0m, result.MaxDrawdownPercent);
    }

    [Fact]
    public void MaxDrawdownPercent_CalculatedCorrectly()
    {
        var trades = new List<Trade>();
        // Peak at 12000, then drops to 9000 = 25% drawdown
        var equityCurve = new List<decimal> { 10000m, 12000m, 9000m, 11000m };

        var result = new BacktestResult(trades, 10000m, 11000m, equityCurve);

        // (12000 - 9000) / 12000 * 100 = 25%
        Assert.Equal(25m, result.MaxDrawdownPercent);
    }

    [Fact]
    public void MaxDrawdownPercent_MultipleDrawdowns_ReturnsMax()
    {
        var trades = new List<Trade>();
        // First drawdown: 12000 -> 10000 = 16.67%
        // Second drawdown: 15000 -> 10500 = 30%
        var equityCurve = new List<decimal> { 10000m, 12000m, 10000m, 15000m, 10500m };

        var result = new BacktestResult(trades, 10000m, 10500m, equityCurve);

        // Max is 30%
        Assert.Equal(30m, result.MaxDrawdownPercent);
    }

    [Fact]
    public void WinRatePercent_NoTrades_ReturnsZero()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10000m, equityCurve);

        Assert.Equal(0m, result.WinRatePercent);
    }

    [Fact]
    public void WinRatePercent_OnlyBuys_ReturnsZero()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 5m, 110m, OrderSide.Buy)
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10000m, equityCurve);

        // No complete round-trips, so win rate is 0
        Assert.Equal(0m, result.WinRatePercent);
    }

    [Fact]
    public void WinRatePercent_AllWinners_Returns100()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 150m, OrderSide.Sell) // +50 profit
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10500m, equityCurve);

        Assert.Equal(100m, result.WinRatePercent);
    }

    [Fact]
    public void WinRatePercent_AllLosers_ReturnsZero()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 80m, OrderSide.Sell) // -20 loss
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 9800m, equityCurve);

        Assert.Equal(0m, result.WinRatePercent);
    }

    [Fact]
    public void WinRatePercent_MixedResults_CalculatedCorrectly()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 150m, OrderSide.Sell), // Win
            new("TEST", 10m, 120m, OrderSide.Buy),
            new("TEST", 10m, 100m, OrderSide.Sell)  // Loss
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10300m, equityCurve);

        // 1 win out of 2 = 50%
        Assert.Equal(50m, result.WinRatePercent);
    }

    [Fact]
    public void ProfitFactor_NoTrades_ReturnsNull()
    {
        var trades = new List<Trade>();
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10000m, equityCurve);

        Assert.Null(result.ProfitFactor);
    }

    [Fact]
    public void ProfitFactor_NoRoundTrips_ReturnsNull()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy)
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10000m, equityCurve);

        Assert.Null(result.ProfitFactor);
    }

    [Fact]
    public void ProfitFactor_NoLosses_ReturnsNull()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 150m, OrderSide.Sell)
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10500m, equityCurve);

        // All wins, no losses to divide by
        Assert.Null(result.ProfitFactor);
    }

    [Fact]
    public void ProfitFactor_CalculatedCorrectly()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 150m, OrderSide.Sell), // +500 profit
            new("TEST", 10m, 120m, OrderSide.Buy),
            new("TEST", 10m, 100m, OrderSide.Sell)  // -200 loss
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10300m, equityCurve);

        // Gross profit = 500, Gross loss = 200
        // Profit factor = 500 / 200 = 2.5
        Assert.Equal(2.5m, result.ProfitFactor);
    }

    [Fact]
    public void TotalTrades_ReturnsCorrectCount()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 150m, OrderSide.Sell),
            new("TEST", 5m, 120m, OrderSide.Buy)
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10500m, equityCurve);

        Assert.Equal(3, result.TotalTrades);
    }

    [Fact]
    public void Trades_ReturnsProvidedList()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 150m, OrderSide.Sell)
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10500m, equityCurve);

        Assert.Equal(2, result.Trades.Count);
        Assert.Equal("TEST", result.Trades[0].Symbol);
        Assert.Equal(OrderSide.Buy, result.Trades[0].Side);
    }

    [Fact]
    public void BreakevenTrades_CountAsNonWinning()
    {
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Buy),
            new("TEST", 10m, 100m, OrderSide.Sell) // Breakeven (P&L = 0)
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10000m, equityCurve);

        // Breakeven is not a win (pnl > 0 is required for win)
        Assert.Equal(0m, result.WinRatePercent);
    }

    [Fact]
    public void UnmatchedSells_AreIgnored()
    {
        // Sell without prior buy should be ignored in metrics
        var trades = new List<Trade>
        {
            new("TEST", 10m, 100m, OrderSide.Sell) // Unmatched sell
        };
        var equityCurve = new List<decimal> { 10000m };

        var result = new BacktestResult(trades, 10000m, 10000m, equityCurve);

        Assert.Equal(0m, result.WinRatePercent);
        Assert.Null(result.ProfitFactor);
    }
}
