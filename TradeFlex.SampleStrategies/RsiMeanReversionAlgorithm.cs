using System.Collections.Generic;
using TradeFlex.Abstractions;
using TradeFlex.Core;

namespace TradeFlex.SampleStrategies;

/// <summary>
/// A mean reversion strategy based on the Relative Strength Index (RSI).
/// Buys when RSI indicates oversold conditions and sells when overbought.
/// </summary>
public sealed class RsiMeanReversionAlgorithm : BaseAlgorithm
{
    private readonly int _rsiPeriod;
    private readonly decimal _oversoldThreshold;
    private readonly decimal _overboughtThreshold;
    private readonly Queue<decimal> _priceChanges = new();
    private decimal _previousClose;
    private bool _hasPreviousClose;

    /// <summary>
    /// Creates the algorithm with default RSI parameters (14-period, 30/70 thresholds).
    /// </summary>
    public RsiMeanReversionAlgorithm() : this(14, 30m, 70m) { }

    /// <summary>
    /// Creates the algorithm with specified RSI parameters.
    /// </summary>
    /// <param name="rsiPeriod">Number of periods for RSI calculation (default 14).</param>
    /// <param name="oversoldThreshold">RSI level below which to buy (default 30).</param>
    /// <param name="overboughtThreshold">RSI level above which to sell (default 70).</param>
    public RsiMeanReversionAlgorithm(int rsiPeriod, decimal oversoldThreshold, decimal overboughtThreshold)
    {
        _rsiPeriod = rsiPeriod;
        _oversoldThreshold = oversoldThreshold;
        _overboughtThreshold = overboughtThreshold;
    }

    /// <inheritdoc />
    public override void Initialize(IAlgorithmContext context)
    {
        base.Initialize(context);
        _priceChanges.Clear();
        _previousClose = 0;
        _hasPreviousClose = false;
    }

    /// <inheritdoc />
    public override void OnBar(Bar bar)
    {
        if (!_hasPreviousClose)
        {
            _previousClose = bar.Close;
            _hasPreviousClose = true;
            return;
        }

        // Calculate price change
        var change = bar.Close - _previousClose;
        _previousClose = bar.Close;

        // Update rolling window of price changes
        _priceChanges.Enqueue(change);
        if (_priceChanges.Count > _rsiPeriod)
        {
            _priceChanges.Dequeue();
        }

        // Need full period to calculate RSI
        if (_priceChanges.Count < _rsiPeriod)
        {
            return;
        }

        var rsi = CalculateRsi();

        if (rsi < _oversoldThreshold)
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
        else if (rsi > _overboughtThreshold)
        {
            // Sell signal: Exit entire position
            var position = Broker.GetPosition(bar.Symbol);
            if (position > 0)
            {
                Sell(bar.Symbol, position);
            }
        }
    }

    /// <summary>
    /// Calculates the RSI based on the rolling window of price changes.
    /// RSI = 100 - (100 / (1 + RS)), where RS = average gain / average loss.
    /// </summary>
    private decimal CalculateRsi()
    {
        decimal totalGain = 0;
        decimal totalLoss = 0;

        foreach (var change in _priceChanges)
        {
            if (change > 0)
            {
                totalGain += change;
            }
            else
            {
                totalLoss += -change; // Make loss positive for calculation
            }
        }

        var avgGain = totalGain / _rsiPeriod;
        var avgLoss = totalLoss / _rsiPeriod;

        // Handle edge case where there are no losses (RS would be infinite)
        if (avgLoss == 0)
        {
            return 100m;
        }

        // Handle edge case where there are no gains
        if (avgGain == 0)
        {
            return 0m;
        }

        var rs = avgGain / avgLoss;
        var rsi = 100m - (100m / (1m + rs));

        return rsi;
    }
}
