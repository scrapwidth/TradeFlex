using Microsoft.ML.Data;

namespace TradeFlex.SampleStrategies.ML;

/// <summary>
/// Feature vector for ML model input containing technical indicators.
/// </summary>
public sealed class FeatureVector
{
    /// <summary>1-bar return (current close / previous close - 1)</summary>
    [LoadColumn(0)]
    public float Return1Bar { get; set; }

    /// <summary>5-bar return</summary>
    [LoadColumn(1)]
    public float Return5Bar { get; set; }

    /// <summary>10-bar return</summary>
    [LoadColumn(2)]
    public float Return10Bar { get; set; }

    /// <summary>Current price / 10-period SMA</summary>
    [LoadColumn(3)]
    public float PriceToSma10 { get; set; }

    /// <summary>Current price / 20-period SMA</summary>
    [LoadColumn(4)]
    public float PriceToSma20 { get; set; }

    /// <summary>14-period RSI (0-100)</summary>
    [LoadColumn(5)]
    public float Rsi14 { get; set; }

    /// <summary>10-period rolling standard deviation of returns</summary>
    [LoadColumn(6)]
    public float Volatility10 { get; set; }

    /// <summary>Current volume / 9-bar average volume</summary>
    [LoadColumn(7)]
    public float VolumeRatio { get; set; }

    /// <summary>(High - Low) / Close</summary>
    [LoadColumn(8)]
    public float HighLowRange { get; set; }

    /// <summary>(Close - Low) / (High - Low) - where close is in the day's range</summary>
    [LoadColumn(9)]
    public float ClosePosition { get; set; }
}
