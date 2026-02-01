# TradeFlex

![Coverage](docs/coverage/badge_shieldsio_linecoverage_green.svg)

TradeFlex is a platform for building, testing, and deploying algorithmic trading strategies for stocks. It supports backtesting on historical data, paper trading with live market feeds via Alpaca, and is designed to eventually support live trading.

## Project Status

| Feature | Status | Notes |
|---------|--------|-------|
| **Backtesting** | ✅ Ready | Full pipeline: download → backtest → analyze trades |
| **Data Download** | ✅ Ready | Alpaca API (requires free API key) |
| **Paper Broker** | ✅ Ready | Realistic fees, fractional quantities, position tracking |
| **Shadow Trading** | ✅ Ready | Live Alpaca data feed with completed minute bars |
| **Alpaca Paper Trading** | ⚠️ Beta | Functional but uses blocking API calls |
| **Alpaca Live Trading** | 🚧 Not Ready | Requires async IBroker refactor |

## Features

- ✅ **Algorithm Framework**: Extensible interface for building trading strategies
- ✅ **Historical Data Download**: Fetch real market data from Alpaca
- ✅ **Backtesting Engine**: Test strategies against historical data with realistic fees
- ✅ **Shadow Trading**: Run strategies against live market data without risking capital
- ✅ **Position Sizing**: Portfolio-based position sizing (10% of capital per trade)
- ✅ **Trading Fees**: Realistic fee simulation (0.5% default, configurable)
- ✅ **Fractional Quantities**: Support for fractional shares

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Alpaca account (free) - [Sign up here](https://alpaca.markets/)

### Installation

```bash
git clone <repository-url>
cd TradeFlex
dotnet build
```

### Configure Alpaca Credentials

```bash
export ALPACA_API_KEY_ID="your_api_key_id"
export ALPACA_SECRET_KEY="your_secret_key"
export ALPACA_USE_PAPER="true"
```

## Usage

### 1. Download Historical Data

Download real market data from Alpaca:

```bash
dotnet run --project TradeFlex.Cli -- download \
  --symbol AAPL \
  --from 2024-01-01 \
  --to 2024-12-31 \
  --granularity 1d \
  --output aapl_2024_daily.parquet
```

**Granularity options**: `1m`, `5m`, `15m`, `1h`, `1d`

**Example output**:
```
Downloading AAPL data from 2024-01-01 to 2024-12-31 via Alpaca...
..
Downloaded 252 bars. Writing to aapl_2024_daily.parquet...
Successfully saved to data/aapl_2024_daily.parquet
```

### 2. Run a Backtest

Test your strategy against historical data:

```bash
dotnet run --project TradeFlex.Cli -- backtest \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --data aapl_2024_daily.parquet \
  --symbol AAPL
```

**Example output**:
```
[PaperBroker] Filled Buy 40.52684904 AAPL @ 246.75. Fee: 50.00. Cash: 89950.00
[PaperBroker] Filled Sell 40.52684904 AAPL @ 251.33. Fee: 50.93. Cash: 100084.52
...
Processed 12 trades
```

**Optional parameters**:
- `--from 2024-06-01`: Start date filter
- `--to 2024-12-31`: End date filter

### 3. Shadow Trading (Paper Trading)

Run your strategy against live market data without risking real money.

#### Option A: In-Memory Simulation

```bash
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol AAPL \
  --broker paper
```

**Features:**
- ✅ Uses Alpaca live data feed
- ✅ Simulated broker with realistic fees
- ✅ No real orders submitted

#### Option B: Alpaca Paper Trading (Real Broker API) ⚠️ Beta

```bash
dotnet run --project TradeFlex.Cli -- shadow \
  --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll \
  --symbol AAPL \
  --broker alpaca
```

**Features:**
- ✅ Orders submitted to Alpaca's paper trading API
- ✅ Orders visible in [Alpaca dashboard](https://app.alpaca.markets/paper/dashboard)
- ✅ Positions synced from real broker account

**Known Limitations:**
- ⚠️ Uses blocking API calls (not suitable for high-frequency trading)
- ⚠️ Assumes 1-second order fill time (no status polling)
- ⚠️ Fractional trading requires enabled Alpaca paper account

**Setup Guide**: See [docs/alpaca_setup.md](docs/alpaca_setup.md) for detailed configuration instructions.

Press `Ctrl+C` to stop.

## Building Your Own Strategy

### 1. Create a New Algorithm

```csharp
using TradeFlex.Abstractions;
using TradeFlex.Core;

public class MyStrategy : BaseAlgorithm
{
    public override void OnBar(Bar bar)
    {
        // Your trading logic here
        var cash = Broker.GetAccountBalance();
        var position = Broker.GetPosition(bar.Symbol);

        // Example: Buy if price drops below $150
        if (bar.Close < 150 && position == 0)
        {
            var quantity = (cash * 0.10m) / bar.Close;  // Use 10% of capital
            Buy(bar.Symbol, quantity);
        }

        // Example: Sell if price rises above $200
        if (bar.Close > 200 && position > 0)
        {
            Sell(bar.Symbol, position);
        }
    }
}
```

### 2. Key Methods

- **`OnBar(Bar bar)`**: Called for each new price bar
- **`Buy(string symbol, decimal quantity)`**: Submit a market buy order
- **`Sell(string symbol, decimal quantity)`**: Submit a market sell order
- **`Broker.GetAccountBalance()`**: Get current cash balance
- **`Broker.GetPosition(string symbol)`**: Get current position size
- **`OnRiskCheck(Order order)`**: Override to add custom risk management

### 3. Bar Data Structure

```csharp
public record Bar(
    string Symbol,      // e.g., "AAPL"
    DateTime Timestamp, // UTC timestamp
    decimal Open,       // Opening price
    decimal High,       // Highest price
    decimal Low,        // Lowest price
    decimal Close,      // Closing price
    long Volume         // Trading volume
);
```

## Sample Strategy: SMA Crossover

The included `SimpleSmaCrossoverAlgorithm` demonstrates:
- **Moving average calculation** (fast and slow SMAs)
- **Crossover detection** (buy on golden cross, sell on death cross)
- **Portfolio-based position sizing** (10% of capital per trade)
- **Position management** (exit entire position on sell signal)

**Configuration**:
```csharp
var algo = new SimpleSmaCrossoverAlgorithm(
    fastPeriod: 5,   // Fast SMA period
    slowPeriod: 20   // Slow SMA period
);
```

## Architecture

```
TradeFlex/
├── TradeFlex.Abstractions/    # Core interfaces (ITradingAlgorithm, IBroker, etc.)
├── TradeFlex.Core/            # Base implementations (BaseAlgorithm, PaperBroker)
├── TradeFlex.BrokerAdapters/  # Broker integrations (Alpaca)
├── TradeFlex.Backtest/        # Backtesting engine and data loaders
├── TradeFlex.SampleStrategies/# Example trading strategies
├── TradeFlex.Cli/             # Command-line interface
└── TradeFlex.Tests/           # Unit tests
```

## Configuration

### Trading Fees

Default fee is 0.5%. Customize when creating the broker:

```csharp
var broker = new PaperBroker(
    initialCash: 100000m,
    feePercentage: 0.001m  // 0.1% fees
);
```

### Position Sizing

The sample strategy uses 10% of available cash per trade. Modify in your algorithm:

```csharp
var dollarAmount = cash * 0.20m;  // Use 20% instead
var quantity = dollarAmount / bar.Close;
Buy(bar.Symbol, quantity);
```

## Performance Metrics

Current backtest output shows:
- Individual trade executions with prices and fees
- Final trade count
- Cash balance changes

## Roadmap

### Completed
- [x] Algorithm interface
- [x] Backtesting engine
- [x] Historical data download via Alpaca
- [x] Paper broker with fees and fractional quantities
- [x] Alpaca paper trading integration (beta)
- [x] Stocks-only focus (removed crypto support)

### In Progress
- [ ] Improve test coverage (currently ~41%)
- [ ] Async IBroker interface for production trading

### Planned
- [ ] Alpaca live trading (requires async refactor)
- [ ] Order status polling and retry logic
- [ ] Performance metrics (Sharpe, drawdown, win rate)
- [ ] Multiple timeframe support
- [ ] Portfolio management (multiple symbols)

## Testing

Run the test suite:

```bash
dotnet test
```

## Contributing

This is a personal project, but suggestions and improvements are welcome!

## License

[Your License Here]

## Disclaimer

This software is for educational purposes only. Trading involves substantial risk of loss. Past performance does not guarantee future results. Use at your own risk.
