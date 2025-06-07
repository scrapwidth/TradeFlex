# TradeFlex

![Coverage](docs/coverage/badge_shieldsio_linecoverage_green.svg)

TradeFlex is a platform designed for individual traders who want to build, test, and eventually deploy algorithmic trading strategies. This repository outlines the initial goals, design considerations, and development roadmap.

## Project Goals

- **Algorithm Interface**: Provide an easily extendable interface so algorithms can be swapped in and out without touching the core infrastructure. Each algorithm should implement standardized entry, exit, and risk management hooks.
- **Backtest Framework**: Integrate with a backtesting engine that can replay historical market data and execute algorithms to validate performance.
- **Manual Testing**: Offer a sandbox where a simple algorithm can be manually run to ensure it interacts with the backtester correctly before moving forward.
- **Unit Tests**: Build a suite of tests verifying the behavior of algorithms and the backtest harness.
- **Shadow Trading**: Design a system that mirrors real orders without executing them, showing what would have happened in a real environment.
- **Live Trading Deployment**: Provide the ability to connect to a real broker so algorithms can trade with actual capital once validated.

## Running the Project (Initial Guidance)

This repository now includes an initial .NET solution (`TradeFlex.sln`) with a core class library. If starting fresh, you could recreate it with the following commands:

```bash
dotnet new sln -n TradeFlex
dotnet new classlib -n TradeFlex.Core
dotnet sln add TradeFlex.Core/TradeFlex.Core.csproj
```

Backtests and algorithms will live under the `TradeFlex` namespace. Example usage might be:

```bash
dotnet run --project TradeFlex.Cli -- backtest --algo path/to/Algo.dll --data path/to/minute.parquet --from 2024-01-01 --to 2024-01-02
```

1. **Generate a Comprehensive Algorithm Interface**
   - Define `ITradingAlgorithm` with hooks such as `OnBar`, `OnEntry`, `OnExit`, and `OnRiskCheck`.
   - Allow runtime discovery of algorithms via an `algorithms` directory so new strategies can be swapped in easily.

### Example Algorithm Interface

Below is a minimal C# implementation of the interface described above. It uses
abstract and virtual methods so each strategy can override the necessary hooks:

```csharp
namespace TradeFlex.Abstractions;

public interface ITradingAlgorithm
{
    void Initialize();
    void OnBar(Bar bar);
    void OnEntry(Order order);
    void OnExit();
    bool OnRiskCheck(Order order);
}
```

Algorithms implementing this interface should live under an `algorithms/`
directory so the framework can discover and load them dynamically at runtime.

For a deeper discussion about how this interface will evolve and how algorithms
are driven by incoming events, see
[docs/algorithm_interface_design.md](docs/algorithm_interface_design.md).

2. **Integrate with a Backtest Framework**
   - Choose an engine (e.g., `Lean`, `StockSharp`, or a minimal custom module) that can feed historical data into algorithms.
   - Ensure the interface is generic enough to plug in future data sources.

3. **Manually Test Simple Algorithm with the Backtester**
   - Create a trivial algorithm (e.g., moving average crossover).
     A ready-made example is provided in `TradeFlex.SampleStrategies`:

     ```csharp
     using TradeFlex.SampleStrategies;

     var algo = new SimpleSmaCrossoverAlgorithm(fastPeriod: 5, slowPeriod: 20);
     ```

   - Execute it against sample historical data, verifying trade signals and order handling.

4. **Add Unit Tests**
   - Use `xUnit` (or a similar .NET testing library) to test algorithm logic and the backtest process.
   - Include tests for edge cases such as empty data sets and erroneous algorithm behavior.

5. **Shadow Trading Design**
   - Implement a mode that publishes trade signals but logs them instead of executing, storing results side-by-side with real account data for comparison.
   - Confirm this mode uses the same decision path as live trading so it is representative.

6. **Enable Live Trading**
   - Add broker integration (e.g., via REST or an SDK) for actual order placement.
   - Include secure storage of API keys and risk checks to limit position sizing.

7. **Iterate and Expand**
   - Document new algorithms.
   - Add performance dashboards and metrics.
   - Improve the backtest environment with additional data sources and risk analysis tools.

This project is in an early state. Follow the roadmap above to incrementally build out the full functionality.
