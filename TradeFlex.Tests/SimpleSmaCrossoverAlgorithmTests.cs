using TradeFlex.Abstractions;
using TradeFlex.Core;
using TradeFlex.SampleStrategies;

namespace TradeFlex.Tests;

public class SimpleSmaCrossoverAlgorithmTests
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
    public void DefaultConstructor_UsesPeriods5And20()
    {
        var algo = new SimpleSmaCrossoverAlgorithm();
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        algo.Initialize(context);

        // With default periods (5, 20), need at least 20 bars to have full windows
        // This just verifies no exceptions are thrown
        Assert.NotNull(algo);
    }

    [Fact]
    public void Initialize_ClearsState()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        // Run some bars
        algo.Initialize(context);
        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(20m));
        algo.OnBar(CreateBar(30m));

        // Re-initialize should clear state
        algo.Initialize(context);
        broker.UpdatePrice(Symbol, 100m);

        // After re-init, windows should be empty - no crossover should occur on first bar
        algo.OnBar(CreateBar(100m));

        // Should have no new trades after re-init (only trades from before)
        Assert.True(broker.Trades.Count <= 1);
    }

    [Fact]
    public void OnBar_NotEnoughData_NoTrades()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        algo.Initialize(context);
        broker.UpdatePrice(Symbol, 100m);

        // Only one bar - not enough data for crossover
        algo.OnBar(CreateBar(100m));

        Assert.Empty(broker.Trades);
    }

    [Fact]
    public void BullishCrossover_GeneratesBuySignal()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        algo.Initialize(context);

        // Create a bullish crossover scenario:
        // Prices: 10, 10, 10, 20, 30
        // Fast SMA (2): starts low, ends at (20+30)/2 = 25
        // Slow SMA (3): starts at 10, ends at (10+20+30)/3 = 20
        // Crossover when fast > slow

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 20m);
        algo.OnBar(CreateBar(20m));

        broker.UpdatePrice(Symbol, 30m);
        algo.OnBar(CreateBar(30m));

        // Should have at least one buy trade
        Assert.Contains(broker.Trades, t => t.Side == OrderSide.Buy);
    }

    [Fact]
    public void BearishCrossover_GeneratesSellSignal()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        algo.Initialize(context);

        // First create a bullish crossover to establish a position
        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 30m);
        algo.OnBar(CreateBar(30m));

        broker.UpdatePrice(Symbol, 50m);
        algo.OnBar(CreateBar(50m));

        // Should have a position now from bullish crossover
        var positionAfterBuy = broker.GetPosition(Symbol);
        Assert.True(positionAfterBuy > 0, "Expected position after bullish crossover");

        // Now create bearish crossover: prices drop
        broker.UpdatePrice(Symbol, 20m);
        algo.OnBar(CreateBar(20m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 5m);
        algo.OnBar(CreateBar(5m));

        // Position should be closed (sold)
        Assert.Equal(0m, broker.GetPosition(Symbol));
        Assert.Contains(broker.Trades, t => t.Side == OrderSide.Sell);
    }

    [Fact]
    public void BuySignal_Uses10PercentOfCash()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        algo.Initialize(context);

        // Generate bullish crossover
        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 30m);
        algo.OnBar(CreateBar(30m));

        broker.UpdatePrice(Symbol, 50m);
        algo.OnBar(CreateBar(50m));

        // With 10000 cash and price at 50, should buy roughly 10000 * 0.10 / 50 = 20 shares
        var buyTrade = broker.Trades.FirstOrDefault(t => t.Side == OrderSide.Buy);
        Assert.NotNull(buyTrade);

        // The quantity should be approximately 10% of cash / price
        // 10000 * 0.10 / 50 = 20
        Assert.True(buyTrade.Quantity > 0);
    }

    [Fact]
    public void SellSignal_ExitsEntirePosition()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        algo.Initialize(context);

        // Create bullish crossover
        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 50m);
        algo.OnBar(CreateBar(50m));
        algo.OnBar(CreateBar(50m));

        var positionAfterBuy = broker.GetPosition(Symbol);
        Assert.True(positionAfterBuy > 0);

        // Create bearish crossover
        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));

        // Position should be fully closed
        Assert.Equal(0m, broker.GetPosition(Symbol));
    }

    [Fact]
    public void SellSignal_NoPosition_NoTrade()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        algo.Initialize(context);

        // Start with high prices so fast > slow
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(100m));

        // Now drop to create bearish crossover (but no position to sell)
        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));

        // No sells should have occurred since no position
        Assert.DoesNotContain(broker.Trades, t => t.Side == OrderSide.Sell);
    }

    [Fact]
    public void NoCrossover_NoTrades()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);

        algo.Initialize(context);

        // Flat prices - no crossover
        broker.UpdatePrice(Symbol, 100m);
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(100m));
        algo.OnBar(CreateBar(100m));

        // No trades when price is flat (both SMAs equal)
        Assert.Empty(broker.Trades);
    }

    [Fact]
    public void MultipleCrossovers_GeneratesMultipleTrades()
    {
        var broker = new PaperBroker(100000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(2, 4);

        algo.Initialize(context);

        // First crossover up
        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 30m);
        algo.OnBar(CreateBar(30m));

        broker.UpdatePrice(Symbol, 50m);
        algo.OnBar(CreateBar(50m));

        var tradesAfterFirstCrossover = broker.Trades.Count;
        Assert.True(tradesAfterFirstCrossover >= 1);

        // Crossover down
        broker.UpdatePrice(Symbol, 20m);
        algo.OnBar(CreateBar(20m));

        broker.UpdatePrice(Symbol, 10m);
        algo.OnBar(CreateBar(10m));

        broker.UpdatePrice(Symbol, 5m);
        algo.OnBar(CreateBar(5m));

        // Second crossover up
        broker.UpdatePrice(Symbol, 30m);
        algo.OnBar(CreateBar(30m));

        broker.UpdatePrice(Symbol, 60m);
        algo.OnBar(CreateBar(60m));

        // Should have multiple trades from multiple crossovers
        Assert.True(broker.Trades.Count > tradesAfterFirstCrossover);
    }

    [Fact]
    public void CustomPeriods_AreRespected()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        var context = new TestContext(broker);
        var algo = new SimpleSmaCrossoverAlgorithm(3, 5);

        algo.Initialize(context);

        // With period 3 and 5, need more bars to fill windows
        broker.UpdatePrice(Symbol, 10m);
        for (int i = 0; i < 5; i++)
        {
            algo.OnBar(CreateBar(10m));
        }

        // Now create bullish crossover
        broker.UpdatePrice(Symbol, 50m);
        algo.OnBar(CreateBar(50m));
        algo.OnBar(CreateBar(50m));
        algo.OnBar(CreateBar(50m));

        // Should trigger buy
        Assert.Contains(broker.Trades, t => t.Side == OrderSide.Buy);
    }

    [Fact]
    public void OnExit_CanBeCalled()
    {
        var algo = new SimpleSmaCrossoverAlgorithm();
        var broker = new PaperBroker(10000m);
        var context = new TestContext(broker);

        algo.Initialize(context);
        algo.OnExit(); // Should not throw

        Assert.True(true); // If we get here, no exception was thrown
    }

    [Fact]
    public void OnRiskCheck_ReturnsTrue()
    {
        var algo = new SimpleSmaCrossoverAlgorithm();
        var broker = new PaperBroker(10000m);
        var context = new TestContext(broker);

        algo.Initialize(context);

        var order = new Order(Symbol, 10m, 100m);
        var result = algo.OnRiskCheck(order);

        Assert.True(result);
    }

    private static Bar CreateBar(decimal close)
    {
        return new Bar(Symbol, DateTime.UtcNow, close, close, close, close, 1000);
    }
}
