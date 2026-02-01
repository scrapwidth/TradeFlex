# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
dotnet build                    # Build all projects
dotnet test                     # Run all tests
dotnet test --filter "FullyQualifiedName~TestName"  # Run single test
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura  # Test with coverage
```

## CLI Commands

Requires Alpaca credentials: `ALPACA_API_KEY_ID`, `ALPACA_SECRET_KEY`, `ALPACA_USE_PAPER`

```bash
# Download historical data from Alpaca
dotnet run --project TradeFlex.Cli -- download --symbol AAPL --from 2024-01-01 --to 2024-12-31 --granularity 1d --output aapl_2024.parquet

# Run backtest against historical data
dotnet run --project TradeFlex.Cli -- backtest --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll --data aapl_2024.parquet --symbol AAPL

# Shadow trading with paper broker
dotnet run --project TradeFlex.Cli -- shadow --algo TradeFlex.SampleStrategies/bin/Debug/net9.0/TradeFlex.SampleStrategies.dll --symbol AAPL --broker paper
```

## Architecture

**Core Abstractions** (`TradeFlex.Abstractions`):
- `ITradingAlgorithm`: Main interface for strategies with `Initialize`, `OnBar`, `OnExit`, `OnRiskCheck` hooks
- `IBroker`: Async order execution interface (`SubmitOrderAsync`, `GetPositionAsync`, `GetAccountBalanceAsync`)
- `IAlgorithmContext`: Injected into algorithms at initialization, provides broker access
- `Bar`, `Order`, `Trade`: Core data records

**Algorithm Development** (`TradeFlex.Core`):
- `BaseAlgorithm`: Abstract base class providing `Buy()`, `Sell()`, and `Broker` access
- `PaperBroker`: Simulated broker with 0.5% fees and fractional quantity support
- Strategies extend `BaseAlgorithm` and implement `OnBar(Bar bar)`

**Execution Flow**:
1. Algorithm DLL loaded via reflection from CLI
2. `AlgorithmRunner.CreateAlgorithm()` instantiates the algorithm type
3. Algorithm receives `IAlgorithmContext` in `Initialize()` with broker reference
4. `OnBar()` called for each price bar (backtest) or completed minute bar (shadow mode)
5. Orders submitted via `BuyAsync()`/`SellAsync()` → `OnRiskCheck()` → `Broker.SubmitOrderAsync()`

**Broker Adapters** (`TradeFlex.BrokerAdapters`):
- `AlpacaBroker`: Real paper/live trading via Alpaca API
- `AlpacaDataFeed`: Live market data via Alpaca WebSocket (minute bars)
- Configuration via environment variables: `ALPACA_API_KEY_ID`, `ALPACA_SECRET_KEY`, `ALPACA_USE_PAPER`

**Data Flow**:
- Historical: Parquet files → `ParquetBarDataLoader` → `BacktestEngine` → Algorithm
- Live: `AlpacaDataFeed` → `ShadowRunner` → Algorithm

## Creating a Strategy

Extend `BaseAlgorithm` and implement `OnBar`:

```csharp
public class MyStrategy : BaseAlgorithm
{
    public override async Task OnBarAsync(Bar bar)
    {
        var cash = await Broker.GetAccountBalanceAsync();
        var position = await Broker.GetPositionAsync(bar.Symbol);

        if (/* buy condition */)
            await BuyAsync(bar.Symbol, quantity);
        if (/* sell condition */)
            await SellAsync(bar.Symbol, position);
    }
}
```

## Test Framework

- xUnit with coverlet for coverage
- CI enforces 70% line coverage threshold
- Tests in `TradeFlex.Tests` project

## Key Design Patterns

**IBroker is fully async**: All broker methods return `Task` or `Task<T>`, enabling non-blocking calls to external APIs like Alpaca.

**Algorithm loading via reflection**: CLI loads strategy DLLs at runtime using `Assembly.LoadFrom()` and finds types implementing `ITradingAlgorithm`. Strategies must be compiled before running.

**Data feeds yield Bar records**: `AlpacaDataFeed` implements `IAsyncEnumerable<Bar>`. The shadow runner consumes these and calls `algorithm.OnBar()` for each completed minute bar.

**PaperBroker tracks state**: Maintains `_positions` dictionary and `_cash` balance. Call `UpdatePrice()` before `SubmitOrder()` to set the fill price.

## Project Dependencies

```
Abstractions (interfaces only, no dependencies)
    ↑
Core (PaperBroker, BaseAlgorithm)
    ↑
BrokerAdapters (AlpacaBroker, AlpacaDataFeed - depends on Alpaca.Markets SDK)
    ↑
Backtest (BacktestEngine, ParquetBarDataLoader)
    ↑
SampleStrategies (example algorithms)
    ↑
Cli (entry point, System.CommandLine)
```
