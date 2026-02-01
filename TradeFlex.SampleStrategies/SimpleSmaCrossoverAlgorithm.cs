using System.Collections.Generic;
using TradeFlex.Abstractions;
using TradeFlex.Core;

namespace TradeFlex.SampleStrategies;

/// <summary>
/// A minimal moving average crossover strategy for demonstration purposes.
/// </summary>
public sealed class SimpleSmaCrossoverAlgorithm : BaseAlgorithm
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly Queue<decimal> _fastWindow = new();
    private readonly Queue<decimal> _slowWindow = new();
    private decimal _previousFast;
    private decimal _previousSlow;

    /// <summary>
    /// Creates the algorithm with default moving average lengths (5, 20).
    /// </summary>
    public SimpleSmaCrossoverAlgorithm() : this(5, 20) { }

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
    public override void Initialize(IAlgorithmContext context)
    {
        base.Initialize(context);
        _fastWindow.Clear();
        _slowWindow.Clear();
        _previousFast = 0;
        _previousSlow = 0;
    }

    /// <inheritdoc />
    public override void OnBar(Bar bar)
    {
        UpdateWindow(_fastWindow, bar.Close, _fastPeriod);
        UpdateWindow(_slowWindow, bar.Close, _slowPeriod);

        var fast = Average(_fastWindow);
        var slow = Average(_slowWindow);

        if (_previousFast <= _previousSlow && fast > slow)
        {
            // Buy signal: Use 10% of available cash
            var cash = Broker.GetAccountBalance();
            var dollarAmount = cash * 0.10m;
            var quantity = dollarAmount / bar.Close;
            
            if (quantity > 0)
            {
                Buy(bar.Symbol, quantity);
            }
        }
        else if (_previousFast >= _previousSlow && fast < slow)
        {
            // Sell signal: Exit entire position
            var position = Broker.GetPosition(bar.Symbol);
            if (position > 0)
            {
                Sell(bar.Symbol, position);
            }
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
}
