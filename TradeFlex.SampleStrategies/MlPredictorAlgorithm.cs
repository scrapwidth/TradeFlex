using TradeFlex.Abstractions;
using TradeFlex.Core;
using TradeFlex.SampleStrategies.ML;

namespace TradeFlex.SampleStrategies;

/// <summary>
/// ML-based trading algorithm using binary classification to predict price direction.
///
/// WARNING: This algorithm is for EDUCATIONAL PURPOSES ONLY. It demonstrates
/// ML integration patterns but should NOT be used for real trading because:
///
/// - OVERFITTING RISK: The model may memorize patterns that don't generalize
///   to new market conditions. Training on historical data and testing on the
///   same data (or similar data) produces misleading results.
///
/// - NON-STATIONARITY: Financial markets change over time. Patterns that worked
///   in one market regime may fail completely in another.
///
/// - TRANSACTION COSTS: Even if the model has a small edge, fees and slippage
///   can easily erase profits. This implementation does not account for these.
///
/// - PAST PERFORMANCE: Historical backtest results do NOT predict future returns.
///   A strategy that looks great in backtests may lose money in live trading.
///
/// Algorithm Flow:
/// - Bars 1-20: Feature warmup (accumulate enough bars for indicator calculation)
/// - Bars 21-300: Training data collection (compute features, record outcomes)
/// - Bar 301: Train ML model on collected data
/// - Bars 302+: Make predictions, trade based on confidence thresholds
/// </summary>
public sealed class MlPredictorAlgorithm : BaseAlgorithm
{
    private readonly int _warmupBars;
    private readonly int _predictionHorizon;
    private readonly float _bullishThreshold;
    private readonly float _bearishThreshold;
    private readonly string? _pretrainedModelPath;

    private readonly FeatureCalculator _featureCalculator = new();
    private readonly TrainingDataBuilder _trainingDataBuilder;
    private readonly PricePredictionModel _model = new();

    private int _barCount;
    private bool _isTraining;

    /// <summary>
    /// Creates an ML predictor algorithm with default parameters.
    /// </summary>
    public MlPredictorAlgorithm()
        : this(warmupBars: 300, predictionHorizon: 5, bullishThreshold: 0.6f, bearishThreshold: 0.4f)
    {
    }

    /// <summary>
    /// Creates an ML predictor algorithm with specified parameters.
    /// </summary>
    /// <param name="warmupBars">Number of bars for training data collection (default 300).</param>
    /// <param name="predictionHorizon">Bars into future to predict (default 5).</param>
    /// <param name="bullishThreshold">Probability above which to buy (default 0.6).</param>
    /// <param name="bearishThreshold">Probability below which to sell (default 0.4).</param>
    /// <param name="pretrainedModelPath">Optional path to pre-trained model file.</param>
    public MlPredictorAlgorithm(
        int warmupBars = 300,
        int predictionHorizon = 5,
        float bullishThreshold = 0.6f,
        float bearishThreshold = 0.4f,
        string? pretrainedModelPath = null)
    {
        _warmupBars = warmupBars;
        _predictionHorizon = predictionHorizon;
        _bullishThreshold = bullishThreshold;
        _bearishThreshold = bearishThreshold;
        _pretrainedModelPath = pretrainedModelPath;

        _trainingDataBuilder = new TrainingDataBuilder(predictionHorizon);
    }

    /// <inheritdoc />
    public override Task InitializeAsync(IAlgorithmContext context)
    {
        base.InitializeAsync(context);

        _barCount = 0;
        _isTraining = true;
        _featureCalculator.Reset();
        _trainingDataBuilder.Reset();

        // Load pre-trained model if specified
        if (!string.IsNullOrEmpty(_pretrainedModelPath) && File.Exists(_pretrainedModelPath))
        {
            _model.LoadModel(_pretrainedModelPath);
            _isTraining = false;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task OnBarAsync(Bar bar)
    {
        _barCount++;

        if (_isTraining)
        {
            await HandleTrainingPhaseAsync(bar);
        }
        else
        {
            await HandlePredictionPhaseAsync(bar);
        }
    }

    private Task HandleTrainingPhaseAsync(Bar bar)
    {
        // Collect training data
        _trainingDataBuilder.AddBar(bar);

        // Train model after warmup period
        if (_barCount >= _warmupBars)
        {
            var trainingData = _trainingDataBuilder.GetTrainingData();

            if (trainingData.Count >= 50)
            {
                _model.Train(trainingData);
                _isTraining = false;
            }
        }

        return Task.CompletedTask;
    }

    private async Task HandlePredictionPhaseAsync(Bar bar)
    {
        // Keep feature calculator updated
        var features = _featureCalculator.AddBar(bar);

        if (features == null)
        {
            return;
        }

        // Get prediction
        var probability = _model.Predict(features);

        // Get current state
        var cash = await Broker.GetAccountBalanceAsync();
        var position = await Broker.GetPositionAsync(bar.Symbol);

        // Trading logic based on probability thresholds
        if (probability > _bullishThreshold && position == 0)
        {
            // Bullish signal with no position: buy 10% of cash
            var dollarAmount = cash * 0.10m;
            var quantity = dollarAmount / bar.Close;

            if (quantity > 0)
            {
                await BuyAsync(bar.Symbol, quantity);
            }
        }
        else if (probability < _bearishThreshold && position > 0)
        {
            // Bearish signal with position: sell all
            await SellAsync(bar.Symbol, position);
        }
    }

    /// <inheritdoc />
    public override Task OnExitAsync()
    {
        // Could save model here if needed
        return Task.CompletedTask;
    }
}
