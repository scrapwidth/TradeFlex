# Algorithm Interface Design

This document describes the proposed structure for algorithms in TradeFlex.

Algorithms must be highly modular so they can run in backtesting, shadow trading, and live trading without modification. To achieve this, we define a small set of public methods that every algorithm must implement. The framework will drive these methods in response to events such as new market data, order executions, and lifecycle transitions.

## Interface Overview

Each algorithm derives from a `BaseAlgorithm` abstract class. The interface defines the following public methods (using C# naming conventions):

- `OnStart(context)`: Optional. Called once before any other method. Allows the algorithm to initialize state or register with the framework. The `context` provides read-only configuration and services (such as a logger or broker handle).

- `OnTick(tick)`: Required. Invoked for every market data update. The `tick` contains price, volume, and any additional market metadata. Algorithms use this event to generate signals, place orders, or update internal indicators.

- `OnOrderFilled(order)`: Required. Triggered whenever an order is executed (either partially or fully). The `order` object includes details like side, quantity, and price. Algorithms update their position and risk calculations here.

- `OnOrderUpdate(order)`: Optional. Called when an order changes status (e.g., from submitted to cancelled). Useful for algorithms that need to track open order states or resubmit orders when they expire.

- `OnExit()`: Required. Invoked when the framework shuts down or the backtest ends. Algorithms should release resources and persist any state here.

- `OnError(error)`: Optional. Receives exceptions or error events so algorithms can gracefully handle issues without crashing the whole system.

## Driving the Algorithms

Algorithms are event-driven. A central engine reads data from the chosen source (historical files for backtests or live feeds for shadow/live trading). The engine dispatches events to the loaded algorithm in the following sequence:

1. Create an instance of the algorithm class and call `OnStart(context)` if the method exists.
2. For each incoming market data tick, call `OnTick(tick)`.
3. When the broker or simulated broker reports an order fill, call `OnOrderFilled(order)`.
4. If an order status changes for reasons other than fill (e.g., cancelled), call `OnOrderUpdate(order)`.
5. At shutdown, invoke `OnExit()`.
6. If an unexpected error occurs during processing, call `OnError(error)` before halting the engine.

By using this small set of hooks, algorithms remain agnostic to the environment driving them. The same class can operate in a backtest or with a real broker as long as the engine supplies the appropriate events.

## Extensibility

The `BaseAlgorithm` interface is intentionally minimal. Additional hooks can be added in the future by creating mixin classes or extending `BaseAlgorithm`. The event-driven model makes it easy to insert new event types without rewriting existing algorithms. For example, a future `OnDailySummary` method could be added for end-of-day processing.

