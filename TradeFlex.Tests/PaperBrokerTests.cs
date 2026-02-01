using TradeFlex.Abstractions;
using TradeFlex.Core;

namespace TradeFlex.Tests;

public class PaperBrokerTests
{
    private const string Symbol = "TEST";

    [Fact]
    public void InitialBalance_ReturnsProvidedCash()
    {
        var broker = new PaperBroker(10000m);
        Assert.Equal(10000m, broker.GetAccountBalance());
    }

    [Fact]
    public void GetPosition_UnknownSymbol_ReturnsZero()
    {
        var broker = new PaperBroker(10000m);
        Assert.Equal(0m, broker.GetPosition("UNKNOWN"));
    }

    [Fact]
    public void GetOpenPositions_InitiallyEmpty()
    {
        var broker = new PaperBroker(10000m);
        Assert.Empty(broker.GetOpenPositions());
    }

    [Fact]
    public void SubmitBuyOrder_UpdatesPositionAndCash()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0.01m); // 1% fee
        broker.UpdatePrice(Symbol, 100m);

        var order = new Order(Symbol, 10m, 0); // Buy 10 shares at market
        broker.SubmitOrder(order);

        // Cost = 10 * 100 = 1000, Fee = 1000 * 0.01 = 10, Total = 1010
        Assert.Equal(10m, broker.GetPosition(Symbol));
        Assert.Equal(10000m - 1010m, broker.GetAccountBalance());
    }

    [Fact]
    public void SubmitSellOrder_UpdatesPositionAndCash()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0.01m); // 1% fee
        broker.UpdatePrice(Symbol, 100m);

        // First buy some shares
        broker.SubmitOrder(new Order(Symbol, 10m, 0));
        var balanceAfterBuy = broker.GetAccountBalance();

        // Sell all shares
        broker.SubmitOrder(new Order(Symbol, -10m, 0));

        // Proceeds = 10 * 100 = 1000, Fee = 10, Net = 990
        Assert.Equal(0m, broker.GetPosition(Symbol));
        Assert.Equal(balanceAfterBuy + 990m, broker.GetAccountBalance());
    }

    [Fact]
    public void SubmitBuyOrder_InsufficientFunds_DoesNotExecute()
    {
        var broker = new PaperBroker(100m, feePercentage: 0.01m);
        broker.UpdatePrice(Symbol, 100m);

        // Try to buy 10 shares (cost would be 1010 with fee, but only have 100)
        var order = new Order(Symbol, 10m, 0);
        broker.SubmitOrder(order);

        Assert.Equal(0m, broker.GetPosition(Symbol));
        Assert.Equal(100m, broker.GetAccountBalance());
    }

    [Fact]
    public void SubmitOrder_WithLimitPrice_UsesLimitPrice()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m); // No fee for simplicity
        broker.UpdatePrice(Symbol, 100m);

        // Buy at limit price of 50
        var order = new Order(Symbol, 10m, 50m);
        broker.SubmitOrder(order);

        // Cost = 10 * 50 = 500 (uses limit price, not market price)
        Assert.Equal(10m, broker.GetPosition(Symbol));
        Assert.Equal(10000m - 500m, broker.GetAccountBalance());
    }

    [Fact]
    public void SubmitMarketOrder_NoPrice_UsesLastPrice()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 200m);

        var order = new Order(Symbol, 5m, 0); // Market order
        broker.SubmitOrder(order);

        // Cost = 5 * 200 = 1000
        Assert.Equal(5m, broker.GetPosition(Symbol));
        Assert.Equal(10000m - 1000m, broker.GetAccountBalance());
    }

    [Fact]
    public void SubmitMarketOrder_NoPriceAvailable_DoesNotExecute()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        // No price set for symbol

        var order = new Order(Symbol, 5m, 0);
        broker.SubmitOrder(order);

        Assert.Equal(0m, broker.GetPosition(Symbol));
        Assert.Equal(10000m, broker.GetAccountBalance());
    }

    [Fact]
    public void FeeCalculation_ZeroFee_NoFeeCharged()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        broker.SubmitOrder(new Order(Symbol, 10m, 0));

        // Cost = 10 * 100 = 1000, no fee
        Assert.Equal(10000m - 1000m, broker.GetAccountBalance());
    }

    [Fact]
    public void FeeCalculation_DefaultFee_ChargesHalfPercent()
    {
        var broker = new PaperBroker(10000m); // Default 0.5% fee
        broker.UpdatePrice(Symbol, 100m);

        broker.SubmitOrder(new Order(Symbol, 10m, 0));

        // Cost = 1000, Fee = 1000 * 0.005 = 5, Total = 1005
        Assert.Equal(10000m - 1005m, broker.GetAccountBalance());
    }

    [Fact]
    public void Trades_RecordsExecutedTrades()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        broker.SubmitOrder(new Order(Symbol, 5m, 0));
        broker.SubmitOrder(new Order(Symbol, -3m, 0));

        Assert.Equal(2, broker.Trades.Count);

        Assert.Equal(Symbol, broker.Trades[0].Symbol);
        Assert.Equal(5m, broker.Trades[0].Quantity);
        Assert.Equal(100m, broker.Trades[0].Price);
        Assert.Equal(OrderSide.Buy, broker.Trades[0].Side);

        Assert.Equal(Symbol, broker.Trades[1].Symbol);
        Assert.Equal(3m, broker.Trades[1].Quantity);
        Assert.Equal(100m, broker.Trades[1].Price);
        Assert.Equal(OrderSide.Sell, broker.Trades[1].Side);
    }

    [Fact]
    public void MultipleSymbols_TrackedIndependently()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice("AAPL", 150m);
        broker.UpdatePrice("GOOG", 100m);

        broker.SubmitOrder(new Order("AAPL", 10m, 0));
        broker.SubmitOrder(new Order("GOOG", 20m, 0));

        Assert.Equal(10m, broker.GetPosition("AAPL"));
        Assert.Equal(20m, broker.GetPosition("GOOG"));

        var positions = broker.GetOpenPositions();
        Assert.Equal(2, positions.Count);
        Assert.Equal(10m, positions["AAPL"]);
        Assert.Equal(20m, positions["GOOG"]);
    }

    [Fact]
    public void FractionalShares_Supported()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        broker.SubmitOrder(new Order(Symbol, 0.5m, 0));

        Assert.Equal(0.5m, broker.GetPosition(Symbol));
        Assert.Equal(10000m - 50m, broker.GetAccountBalance());
    }

    [Fact]
    public void UpdatePrice_OverwritesPreviousPrice()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);
        broker.UpdatePrice(Symbol, 200m);

        broker.SubmitOrder(new Order(Symbol, 1m, 0));

        // Should use the updated price of 200
        Assert.Equal(10000m - 200m, broker.GetAccountBalance());
    }

    [Fact]
    public void SellingReducesPosition_CanGoToZero()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        broker.SubmitOrder(new Order(Symbol, 10m, 0));
        broker.SubmitOrder(new Order(Symbol, -5m, 0));
        Assert.Equal(5m, broker.GetPosition(Symbol));

        broker.SubmitOrder(new Order(Symbol, -5m, 0));
        Assert.Equal(0m, broker.GetPosition(Symbol));
    }

    [Fact]
    public void BuyOrder_ExactFundsAvailable_ExecutesSuccessfully()
    {
        // With fee 0.01, buying 10 shares at 100 costs 1010
        var broker = new PaperBroker(1010m, feePercentage: 0.01m);
        broker.UpdatePrice(Symbol, 100m);

        broker.SubmitOrder(new Order(Symbol, 10m, 0));

        Assert.Equal(10m, broker.GetPosition(Symbol));
        Assert.Equal(0m, broker.GetAccountBalance());
    }
}
