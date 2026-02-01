using TradeFlex.Abstractions;

namespace TradeFlex.SampleStrategies.ML;

/// <summary>
/// Builds training data by pairing features with future price outcomes.
/// </summary>
public sealed class TrainingDataBuilder
{
    private readonly int _predictionHorizon;
    private readonly FeatureCalculator _featureCalculator = new();
    private readonly Queue<(FeatureVector Features, decimal ClosePrice)> _pendingLabels = new();
    private readonly List<TrainingExample> _trainingData = new();

    /// <summary>
    /// Creates a training data builder.
    /// </summary>
    /// <param name="predictionHorizon">Number of bars into the future to predict.</param>
    public TrainingDataBuilder(int predictionHorizon = 5)
    {
        _predictionHorizon = predictionHorizon;
    }

    /// <summary>
    /// Number of training examples collected.
    /// </summary>
    public int TrainingExampleCount => _trainingData.Count;

    /// <summary>
    /// Whether the feature calculator has enough bars.
    /// </summary>
    public bool IsFeatureReady => _featureCalculator.IsReady;

    /// <summary>
    /// Adds a bar and potentially generates a training example.
    /// </summary>
    public void AddBar(Bar bar)
    {
        var features = _featureCalculator.AddBar(bar);

        if (features != null)
        {
            _pendingLabels.Enqueue((features, bar.Close));
        }

        // Check if we can label any pending examples
        while (_pendingLabels.Count > _predictionHorizon)
        {
            var (pendingFeatures, entryPrice) = _pendingLabels.Dequeue();
            var futurePrice = bar.Close;
            var label = futurePrice > entryPrice;

            _trainingData.Add(new TrainingExample
            {
                Features = pendingFeatures,
                Label = label
            });
        }
    }

    /// <summary>
    /// Gets all collected training examples.
    /// </summary>
    public IReadOnlyList<TrainingExample> GetTrainingData() => _trainingData;

    /// <summary>
    /// Clears all collected data.
    /// </summary>
    public void Reset()
    {
        _featureCalculator.Reset();
        _pendingLabels.Clear();
        _trainingData.Clear();
    }
}

/// <summary>
/// A training example pairing features with a label.
/// </summary>
public sealed class TrainingExample
{
    /// <summary>Input features.</summary>
    public required FeatureVector Features { get; init; }

    /// <summary>True if price went up after prediction horizon, false otherwise.</summary>
    public bool Label { get; init; }
}
