using Microsoft.ML;
using Microsoft.ML.Data;

namespace TradeFlex.SampleStrategies.ML;

/// <summary>
/// ML.NET wrapper for binary price direction prediction using FastTree.
///
/// WARNING: This model is for educational purposes only. Real-world ML trading
/// faces serious challenges:
/// - Overfitting: The model may memorize patterns that don't generalize
/// - Non-stationarity: Market regimes change, invalidating learned patterns
/// - Transaction costs: Small predicted edges are often erased by fees
/// - Past performance does not predict future results
/// </summary>
public sealed class PricePredictionModel
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private PredictionEngine<ModelInput, ModelOutput>? _predictionEngine;

    public PricePredictionModel()
    {
        _mlContext = new MLContext(seed: 42);
    }

    /// <summary>
    /// Whether the model has been trained.
    /// </summary>
    public bool IsTrained => _model != null;

    /// <summary>
    /// Trains the model on the provided training examples.
    /// </summary>
    public void Train(IReadOnlyList<TrainingExample> trainingData)
    {
        if (trainingData.Count < 50)
        {
            throw new InvalidOperationException(
                $"Need at least 50 training examples, got {trainingData.Count}. " +
                "Collect more bars before training.");
        }

        var inputs = trainingData.Select(t => new ModelInput
        {
            Return1Bar = t.Features.Return1Bar,
            Return5Bar = t.Features.Return5Bar,
            Return10Bar = t.Features.Return10Bar,
            PriceToSma10 = t.Features.PriceToSma10,
            PriceToSma20 = t.Features.PriceToSma20,
            Rsi14 = t.Features.Rsi14,
            Volatility10 = t.Features.Volatility10,
            VolumeRatio = t.Features.VolumeRatio,
            HighLowRange = t.Features.HighLowRange,
            ClosePosition = t.Features.ClosePosition,
            Label = t.Label
        }).ToList();

        var dataView = _mlContext.Data.LoadFromEnumerable(inputs);

        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(ModelInput.Return1Bar),
                nameof(ModelInput.Return5Bar),
                nameof(ModelInput.Return10Bar),
                nameof(ModelInput.PriceToSma10),
                nameof(ModelInput.PriceToSma20),
                nameof(ModelInput.Rsi14),
                nameof(ModelInput.Volatility10),
                nameof(ModelInput.VolumeRatio),
                nameof(ModelInput.HighLowRange),
                nameof(ModelInput.ClosePosition))
            .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: nameof(ModelInput.Label),
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 10));

        _model = pipeline.Fit(dataView);
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
    }

    /// <summary>
    /// Predicts the probability that price will go up.
    /// </summary>
    /// <param name="features">Input features.</param>
    /// <returns>Probability between 0 and 1.</returns>
    public float Predict(FeatureVector features)
    {
        if (_predictionEngine == null)
        {
            throw new InvalidOperationException("Model not trained. Call Train() first.");
        }

        var input = new ModelInput
        {
            Return1Bar = features.Return1Bar,
            Return5Bar = features.Return5Bar,
            Return10Bar = features.Return10Bar,
            PriceToSma10 = features.PriceToSma10,
            PriceToSma20 = features.PriceToSma20,
            Rsi14 = features.Rsi14,
            Volatility10 = features.Volatility10,
            VolumeRatio = features.VolumeRatio,
            HighLowRange = features.HighLowRange,
            ClosePosition = features.ClosePosition
        };

        var output = _predictionEngine.Predict(input);
        return output.Probability;
    }

    /// <summary>
    /// Saves the trained model to a file.
    /// </summary>
    public void SaveModel(string path)
    {
        if (_model == null)
        {
            throw new InvalidOperationException("Model not trained. Call Train() first.");
        }

        _mlContext.Model.Save(_model, null, path);
    }

    /// <summary>
    /// Loads a pre-trained model from a file.
    /// </summary>
    public void LoadModel(string path)
    {
        _model = _mlContext.Model.Load(path, out _);
        _predictionEngine = _mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
    }

    /// <summary>
    /// ML.NET input schema.
    /// </summary>
    private sealed class ModelInput
    {
        public float Return1Bar { get; set; }
        public float Return5Bar { get; set; }
        public float Return10Bar { get; set; }
        public float PriceToSma10 { get; set; }
        public float PriceToSma20 { get; set; }
        public float Rsi14 { get; set; }
        public float Volatility10 { get; set; }
        public float VolumeRatio { get; set; }
        public float HighLowRange { get; set; }
        public float ClosePosition { get; set; }
        public bool Label { get; set; }
    }

    /// <summary>
    /// ML.NET output schema.
    /// </summary>
    private sealed class ModelOutput
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedLabel { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
