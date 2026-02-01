using TradeFlex.Abstractions;

namespace TradeFlex.SampleStrategies.ML;

/// <summary>
/// Computes technical indicator features from price bars for ML model input.
///
/// WARNING: This is for educational purposes only. ML-based trading faces
/// significant challenges including overfitting, non-stationarity of markets,
/// and transaction costs eroding small edges.
/// </summary>
public sealed class FeatureCalculator
{
    private const int MinBarsRequired = 20;
    private readonly List<Bar> _bars = new();

    /// <summary>
    /// Minimum number of bars needed before features can be calculated.
    /// </summary>
    public int WarmupBarsRequired => MinBarsRequired;

    /// <summary>
    /// Current number of bars accumulated.
    /// </summary>
    public int BarCount => _bars.Count;

    /// <summary>
    /// Whether enough bars have been accumulated to calculate features.
    /// </summary>
    public bool IsReady => _bars.Count >= MinBarsRequired;

    /// <summary>
    /// Adds a bar and returns features if enough data is available.
    /// </summary>
    public FeatureVector? AddBar(Bar bar)
    {
        _bars.Add(bar);

        if (!IsReady)
            return null;

        return ComputeFeatures();
    }

    /// <summary>
    /// Gets the most recent bar.
    /// </summary>
    public Bar? CurrentBar => _bars.Count > 0 ? _bars[^1] : null;

    private FeatureVector ComputeFeatures()
    {
        var current = _bars[^1];
        var prev1 = _bars[^2];
        var prev5 = _bars[^6];
        var prev10 = _bars[^11];

        return new FeatureVector
        {
            Return1Bar = CalculateReturn(current.Close, prev1.Close),
            Return5Bar = CalculateReturn(current.Close, prev5.Close),
            Return10Bar = CalculateReturn(current.Close, prev10.Close),
            PriceToSma10 = CalculatePriceToSma(10),
            PriceToSma20 = CalculatePriceToSma(20),
            Rsi14 = CalculateRsi(14),
            Volatility10 = CalculateVolatility(10),
            VolumeRatio = CalculateVolumeRatio(),
            HighLowRange = (float)((current.High - current.Low) / current.Close),
            ClosePosition = (float)((current.Close - current.Low) / (current.High - current.Low + 0.0001m))
        };
    }

    private static float CalculateReturn(decimal current, decimal previous)
    {
        if (previous == 0) return 0;
        return (float)((current - previous) / previous);
    }

    private float CalculatePriceToSma(int period)
    {
        if (_bars.Count < period) return 1;

        var sum = 0m;
        for (int i = _bars.Count - period; i < _bars.Count; i++)
        {
            sum += _bars[i].Close;
        }
        var sma = sum / period;
        return sma == 0 ? 1 : (float)(_bars[^1].Close / sma);
    }

    private float CalculateRsi(int period)
    {
        if (_bars.Count < period + 1) return 50;

        decimal totalGain = 0;
        decimal totalLoss = 0;

        for (int i = _bars.Count - period; i < _bars.Count; i++)
        {
            var change = _bars[i].Close - _bars[i - 1].Close;
            if (change > 0)
                totalGain += change;
            else
                totalLoss -= change;
        }

        var avgGain = totalGain / period;
        var avgLoss = totalLoss / period;

        if (avgLoss == 0) return 100;
        if (avgGain == 0) return 0;

        var rs = avgGain / avgLoss;
        return (float)(100m - (100m / (1m + rs)));
    }

    private float CalculateVolatility(int period)
    {
        if (_bars.Count < period) return 0;

        var returns = new List<decimal>();
        for (int i = _bars.Count - period; i < _bars.Count; i++)
        {
            if (i > 0 && _bars[i - 1].Close != 0)
            {
                returns.Add((_bars[i].Close - _bars[i - 1].Close) / _bars[i - 1].Close);
            }
        }

        if (returns.Count < 2) return 0;

        var mean = returns.Average();
        var sumSquaredDiff = returns.Sum(r => (r - mean) * (r - mean));
        var variance = sumSquaredDiff / (returns.Count - 1);
        return (float)Math.Sqrt((double)variance);
    }

    private float CalculateVolumeRatio()
    {
        if (_bars.Count < 10) return 1;

        var currentVolume = _bars[^1].Volume;
        var avgVolume = 0m;
        for (int i = _bars.Count - 10; i < _bars.Count - 1; i++)
        {
            avgVolume += _bars[i].Volume;
        }
        avgVolume /= 9;

        return avgVolume == 0 ? 1 : (float)(currentVolume / avgVolume);
    }

    /// <summary>
    /// Clears all accumulated bars.
    /// </summary>
    public void Reset()
    {
        _bars.Clear();
    }
}
