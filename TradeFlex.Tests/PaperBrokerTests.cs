using System.Threading.Tasks;
using TradeFlex.Abstractions;
using TradeFlex.Core;

namespace TradeFlex.Tests;

public class PaperBrokerTests
{
    private const string Symbol = "TEST";

    [Fact]
    public async Task InitialBalance_ReturnsProvidedCash()
    {
        var broker = new PaperBroker(10000m);
        Assert.Equal(10000m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task GetPosition_UnknownSymbol_ReturnsZero()
    {
        var broker = new PaperBroker(10000m);
        Assert.Equal(0m, await broker.GetPositionAsync("UNKNOWN"));
    }

    [Fact]
    public async Task GetOpenPositions_InitiallyEmpty()
    {
        var broker = new PaperBroker(10000m);
        Assert.Empty(await broker.GetOpenPositionsAsync());
    }

    [Fact]
    public async Task SubmitBuyOrder_UpdatesPositionAndCash()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0.01m); // 1% fee
        broker.UpdatePrice(Symbol, 100m);

        var order = new Order(Symbol, 10m, 0); // Buy 10 shares at market
        await broker.SubmitOrderAsync(order);

        // Cost = 10 * 100 = 1000, Fee = 1000 * 0.01 = 10, Total = 1010
        Assert.Equal(10m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(10000m - 1010m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task SubmitSellOrder_UpdatesPositionAndCash()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0.01m); // 1% fee
        broker.UpdatePrice(Symbol, 100m);

        // First buy some shares
        await broker.SubmitOrderAsync(new Order(Symbol, 10m, 0));
        var balanceAfterBuy = await broker.GetAccountBalanceAsync();

        // Sell all shares
        await broker.SubmitOrderAsync(new Order(Symbol, -10m, 0));

        // Proceeds = 10 * 100 = 1000, Fee = 10, Net = 990
        Assert.Equal(0m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(balanceAfterBuy + 990m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task SubmitBuyOrder_InsufficientFunds_DoesNotExecute()
    {
        var broker = new PaperBroker(100m, feePercentage: 0.01m);
        broker.UpdatePrice(Symbol, 100m);

        // Try to buy 10 shares (cost would be 1010 with fee, but only have 100)
        var order = new Order(Symbol, 10m, 0);
        await broker.SubmitOrderAsync(order);

        Assert.Equal(0m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(100m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task SubmitOrder_WithLimitPrice_UsesLimitPrice()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m); // No fee for simplicity
        broker.UpdatePrice(Symbol, 100m);

        // Buy at limit price of 50
        var order = new Order(Symbol, 10m, 50m);
        await broker.SubmitOrderAsync(order);

        // Cost = 10 * 50 = 500 (uses limit price, not market price)
        Assert.Equal(10m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(10000m - 500m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task SubmitMarketOrder_NoPrice_UsesLastPrice()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 200m);

        var order = new Order(Symbol, 5m, 0); // Market order
        await broker.SubmitOrderAsync(order);

        // Cost = 5 * 200 = 1000
        Assert.Equal(5m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(10000m - 1000m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task SubmitMarketOrder_NoPriceAvailable_DoesNotExecute()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        // No price set for symbol

        var order = new Order(Symbol, 5m, 0);
        await broker.SubmitOrderAsync(order);

        Assert.Equal(0m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(10000m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task FeeCalculation_ZeroFee_NoFeeCharged()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        await broker.SubmitOrderAsync(new Order(Symbol, 10m, 0));

        // Cost = 10 * 100 = 1000, no fee
        Assert.Equal(10000m - 1000m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task FeeCalculation_DefaultFee_ChargesHalfPercent()
    {
        var broker = new PaperBroker(10000m); // Default 0.5% fee
        broker.UpdatePrice(Symbol, 100m);

        await broker.SubmitOrderAsync(new Order(Symbol, 10m, 0));

        // Cost = 1000, Fee = 1000 * 0.005 = 5, Total = 1005
        Assert.Equal(10000m - 1005m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task Trades_RecordsExecutedTrades()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        await broker.SubmitOrderAsync(new Order(Symbol, 5m, 0));
        await broker.SubmitOrderAsync(new Order(Symbol, -3m, 0));

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
    public async Task MultipleSymbols_TrackedIndependently()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice("AAPL", 150m);
        broker.UpdatePrice("GOOG", 100m);

        await broker.SubmitOrderAsync(new Order("AAPL", 10m, 0));
        await broker.SubmitOrderAsync(new Order("GOOG", 20m, 0));

        Assert.Equal(10m, await broker.GetPositionAsync("AAPL"));
        Assert.Equal(20m, await broker.GetPositionAsync("GOOG"));

        var positions = await broker.GetOpenPositionsAsync();
        Assert.Equal(2, positions.Count);
        Assert.Equal(10m, positions["AAPL"]);
        Assert.Equal(20m, positions["GOOG"]);
    }

    [Fact]
    public async Task FractionalShares_Supported()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        await broker.SubmitOrderAsync(new Order(Symbol, 0.5m, 0));

        Assert.Equal(0.5m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(10000m - 50m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task UpdatePrice_OverwritesPreviousPrice()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);
        broker.UpdatePrice(Symbol, 200m);

        await broker.SubmitOrderAsync(new Order(Symbol, 1m, 0));

        // Should use the updated price of 200
        Assert.Equal(10000m - 200m, await broker.GetAccountBalanceAsync());
    }

    [Fact]
    public async Task SellingReducesPosition_CanGoToZero()
    {
        var broker = new PaperBroker(10000m, feePercentage: 0m);
        broker.UpdatePrice(Symbol, 100m);

        await broker.SubmitOrderAsync(new Order(Symbol, 10m, 0));
        await broker.SubmitOrderAsync(new Order(Symbol, -5m, 0));
        Assert.Equal(5m, await broker.GetPositionAsync(Symbol));

        await broker.SubmitOrderAsync(new Order(Symbol, -5m, 0));
        Assert.Equal(0m, await broker.GetPositionAsync(Symbol));
    }

    [Fact]
    public async Task BuyOrder_ExactFundsAvailable_ExecutesSuccessfully()
    {
        // With fee 0.01, buying 10 shares at 100 costs 1010
        var broker = new PaperBroker(1010m, feePercentage: 0.01m);
        broker.UpdatePrice(Symbol, 100m);

        await broker.SubmitOrderAsync(new Order(Symbol, 10m, 0));

        Assert.Equal(10m, await broker.GetPositionAsync(Symbol));
        Assert.Equal(0m, await broker.GetAccountBalanceAsync());
    }
}
