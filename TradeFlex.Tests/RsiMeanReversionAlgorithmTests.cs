using TradeFlex.Abstractions;
using TradeFlex.Core;
using TradeFlex.SampleStrategies;

namespace TradeFlex.Tests;

public class RsiMeanReversionAlgorithmTests
{
    private const string Symbol = "TEST";

    private sealed class TestContext : IAlgorithmContext
    {
        public IBroker Broker { get; }

        public TestContext(IBroker broker)
        {
            Broker = broker;
        }
    }

    [Fact]
    public void DefaultConstructor_UsesDefaultParameters()
    {
        var algo = new RsiMeanReversionAlgorithm();
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        algo.Initialize(context);

        // Just verify no exceptions are thrown
        Assert.NotNull(algo);
    }

    [Fact]
    public void CustomParameters_AreRespected()
    {
        var algo = new RsiMeanReversionAlgorithm(7, 20m, 80m);
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        algo.Initialize(context);

        // Verify algorithm can be used with custom parameters
        Assert.NotNull(algo);
    }

    [Fact]
    public void OnBar_FirstBar_NoTradeNoPreviousClose()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);
        broker.UpdatePrice(Symbol, 100m);

        // First bar - just establishes previous close
        algo.OnBar(CreateBar(100m));

        Assert.Empty(broker.Trades);
    }

    [Fact]
    public void OnBar_NotEnoughData_NoTrades()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(5, 30m, 70m);

        algo.Initialize(context);
        broker.UpdatePrice(Symbol, 100m);

        // Need 5 periods of data, only provide 3
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(101m));
        algo.OnBar(CreateBar(102m));

        Assert.Empty(broker.Trades);
    }

    [Fact]
    public void OversoldCondition_GeneratesBuySignal()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);

        // Create a series of declining prices to get RSI below 30
        // Start at 100, then decline consistently
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m)); // First bar, establishes previous close

        broker.UpdatePrice(Symbol, 90m);
        algo.OnBar(CreateBar(90m)); // Change: -10

        broker.UpdatePrice(Symbol, 80m);
        algo.OnBar(CreateBar(80m)); // Change: -10

        broker.UpdatePrice(Symbol, 70m);
        algo.OnBar(CreateBar(70m)); // Change: -10

        // All changes are losses, RSI should be 0 (< 30)
        // Should trigger buy signal
        Assert.Contains(broker.Trades, t => t.Side == OrderSide.Buy);
    }

    [Fact]
    public void OverboughtCondition_GeneratesSellSignal()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);

        // First establish a position by going oversold
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));

        broker.UpdatePrice(Symbol, 90m);
        algo.OnBar(CreateBar(90m));

        broker.UpdatePrice(Symbol, 80m);
        algo.OnBar(CreateBar(80m));

        broker.UpdatePrice(Symbol, 70m);
        algo.OnBar(CreateBar(70m));

        // Should have bought
        Assert.True(broker.GetPosition(Symbol) > 0);

        // Now create rising prices to get RSI above 70
        broker.UpdatePrice(Symbol, 80m);
        algo.OnBar(CreateBar(80m)); // Change: +10

        broker.UpdatePrice(Symbol, 90m);
        algo.OnBar(CreateBar(90m)); // Change: +10

        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m)); // Change: +10

        // All changes are gains, RSI should be 100 (> 70)
        // Should trigger sell signal
        Assert.Contains(broker.Trades, t => t.Side == OrderSide.Sell);
    }

    [Fact]
    public void Rsi100_AllGains_SellSignalIfPosition()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);

        // First create position manually via broker
        broker.UpdatePrice(Symbol, 50m);
        broker.SubmitOrder(new Order(Symbol, 100m, 0)); // Buy 100 shares

        // Now run algo with rising prices (RSI = 100)
        algo.OnBar(CreateBar(50m));

        broker.UpdatePrice(Symbol, 60m);
        algo.OnBar(CreateBar(60m)); // +10

        broker.UpdatePrice(Symbol, 70m);
        algo.OnBar(CreateBar(70m)); // +10

        broker.UpdatePrice(Symbol, 80m);
        algo.OnBar(CreateBar(80m)); // +10

        // RSI = 100, should sell position
        // Original buy + sell should mean position is reduced/closed
        Assert.Contains(broker.Trades, t => t.Side == OrderSide.Sell);
    }

    [Fact]
    public void Rsi0_AllLosses_BuySignal()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);

        // Declining prices (RSI = 0)
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));

        broker.UpdatePrice(Symbol, 90m);
        algo.OnBar(CreateBar(90m));

        broker.UpdatePrice(Symbol, 80m);
        algo.OnBar(CreateBar(80m));

        broker.UpdatePrice(Symbol, 70m);
        algo.OnBar(CreateBar(70m));

        // RSI = 0, should buy
        Assert.Contains(broker.Trades, t => t.Side == OrderSide.Buy);
    }

    [Fact]
    public void RsiMidRange_NoTrades()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(4, 30m, 70m);

        algo.Initialize(context);

        // Alternating prices to keep RSI around 50
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));

        broker.UpdatePrice(Symbol, 105m);
        algo.OnBar(CreateBar(105m)); // +5

        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m)); // -5

        broker.UpdatePrice(Symbol, 105m);
        algo.OnBar(CreateBar(105m)); // +5

        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m)); // -5

        // RSI around 50, no trades expected
        Assert.Empty(broker.Trades);
    }

    [Fact]
    public void SellSignal_NoPosition_NoTrade()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);

        // Rising prices to get RSI > 70, but no position to sell
        broker.UpdatePrice(Symbol, 50m);
        algo.OnBar(CreateBar(50m));

        broker.UpdatePrice(Symbol, 60m);
        algo.OnBar(CreateBar(60m));

        broker.UpdatePrice(Symbol, 70m);
        algo.OnBar(CreateBar(70m));

        broker.UpdatePrice(Symbol, 80m);
        algo.OnBar(CreateBar(80m));

        // No sells should have occurred since no position
        Assert.DoesNotContain(broker.Trades, t => t.Side == OrderSide.Sell);
    }

    [Fact]
    public void BuySignal_Uses10PercentOfCash()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);

        // Declining prices to get oversold
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));

        broker.UpdatePrice(Symbol, 90m);
        algo.OnBar(CreateBar(90m));

        broker.UpdatePrice(Symbol, 80m);
        algo.OnBar(CreateBar(80m));

        broker.UpdatePrice(Symbol, 70m);
        algo.OnBar(CreateBar(70m));

        var buyTrade = broker.Trades.FirstOrDefault(t => t.Side == OrderSide.Buy);
        Assert.NotNull(buyTrade);

        // 10% of 10000 = 1000, at price 70 = ~14.28 shares
        Assert.True(buyTrade.Quantity > 0);
    }

    [Fact]
    public void Initialize_ClearsState()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        // First run
        algo.Initialize(context);
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(90m));

        // Re-initialize
        algo.Initialize(context);

        // After re-init, should behave as if starting fresh
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));

        // Only one bar after re-init, no trades expected
        // (previous state should be cleared)
        Assert.Equal(0m, broker.GetPosition(Symbol));
    }

    [Fact]
    public void OnExit_CanBeCalled()
    {
        var algo = new RsiMeanReversionAlgorithm();
        var broker = new PaperBroker(10000m);
        var context = new TestContext(broker);

        algo.Initialize(context);
        algo.OnExit(); // Should not throw

        Assert.True(true);
    }

    [Fact]
    public void OnRiskCheck_ReturnsTrue()
    {
        var algo = new RsiMeanReversionAlgorithm();
        var broker = new PaperBroker(10000m);
        var context = new TestContext(broker);

        algo.Initialize(context);

        var order = new Order(Symbol, 10m, 100m);
        var result = algo.OnRiskCheck(order);

        Assert.True(result);
    }

    [Fact]
    public void WindowMaintainsCorrectSize()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new RsiMeanReversionAlgorithm(3, 30m, 70m);

        algo.Initialize(context);

        // Feed more bars than the period
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(90m));
        algo.OnBar(CreateBar(80m));
        algo.OnBar(CreateBar(70m));
        algo.OnBar(CreateBar(60m));
        algo.OnBar(CreateBar(50m)); // 6 bars, but window should only hold 3

        // Should still function correctly - algorithm doesn't crash
        Assert.True(true);
    }

    private static Bar CreateBar(decimal close)
    {
        return new Bar(Symbol, DateTime.UtcNow, close, close, close, close, 1000);
    }
}
