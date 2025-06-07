using System.Collections.Generic;
using TradeFlex.Abstractions;

namespace TradeFlex.SampleStrategies;

/// <summary>
/// A minimal moving average crossover strategy for demonstration purposes.
/// </summary>
public sealed class SimpleSmaCrossoverAlgorithm : ITradingAlgorithm
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly Queue<decimal> _fastWindow = new();
    private readonly Queue<decimal> _slowWindow = new();
    private decimal _previousFast;
    private decimal _previousSlow;

    /// <summary>
    /// Creates the algorithm with specified moving average lengths.
    /// </summary>
    /// <param name="fastPeriod">Lookback for the fast SMA.</param>
    /// <param name="slowPeriod">Lookback for the slow SMA.</param>
    public SimpleSmaCrossoverAlgorithm(int fastPeriod, int slowPeriod)
    {
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
    }

    /// <inheritdoc />
    public void Initialize()
    {
        _fastWindow.Clear();
        _slowWindow.Clear();
        _previousFast = 0;
        _previousSlow = 0;
    }

    /// <inheritdoc />
    public void OnBar(Bar bar)
    {
        UpdateWindow(_fastWindow, bar.Close, _fastPeriod);
        UpdateWindow(_slowWindow, bar.Close, _slowPeriod);

        var fast = Average(_fastWindow);
        var slow = Average(_slowWindow);

        if (_previousFast <= _previousSlow && fast > slow)
        {
            OnEntry(new Order("SAMPLE", 1, bar.Close));
        }
        else if (_previousFast >= _previousSlow && fast < slow)
        {
            OnExit();
        }

        _previousFast = fast;
        _previousSlow = slow;
    }

    private static void UpdateWindow(Queue<decimal> window, decimal value, int period)
    {
        window.Enqueue(value);
        if (window.Count > period)
        {
            window.Dequeue();
        }
    }

    private static decimal Average(IEnumerable<decimal> values)
    {
        decimal sum = 0;
        int count = 0;
        foreach (var v in values)
        {
            sum += v;
            count++;
        }
        return count == 0 ? 0 : sum / count;
    }

    /// <inheritdoc />
    public void OnEntry(Order order)
    {
        // In a real implementation this would submit the order via a broker adapter.
    }

    /// <inheritdoc />
    public void OnExit()
    {
        // Cleanup or exit logic would go here.
    }

    /// <inheritdoc />
    public bool OnRiskCheck(Order order) => true;
}

