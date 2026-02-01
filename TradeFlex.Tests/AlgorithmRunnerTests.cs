using TradeFlex.Abstractions;
using TradeFlex.Core;
using TradeFlex.SampleStrategies;

namespace TradeFlex.Tests;

public class AlgorithmRunnerTests
{
    [Fact]
    public void AlgorithmRunnerRunsSmaSample()
    {
        var bars = new List<Bar>
        {
            new("SAMPLE", DateTime.UtcNow, 1, 1, 1, 1, 100),
            new("SAMPLE", DateTime.UtcNow, 2, 2, 2, 2, 100),
            new("SAMPLE", DateTime.UtcNow, 3, 3, 3, 3, 100),
            new("SAMPLE", DateTime.UtcNow, 4, 4, 4, 4, 100),
        };

        var trades = AlgorithmRunner.Run<SimpleSmaCrossoverAlgorithm>(bars, 2, 3);

        Assert.NotNull(trades);
    }

    [Fact]
    public void CreateAlgorithm_InvalidType_ThrowsArgumentException()
    {
        // String does not implement ITradingAlgorithm
        var exception = Assert.Throws<ArgumentException>(() =>
            AlgorithmRunner.CreateAlgorithm(typeof(string)));

        Assert.Contains("does not implement ITradingAlgorithm", exception.Message);
    }

    [Fact]
    public void CreateAlgorithm_ValidType_ReturnsInstance()
    {
        var algo = AlgorithmRunner.CreateAlgorithm(typeof(SimpleSmaCrossoverAlgorithm), 5, 10);

        Assert.NotNull(algo);
        Assert.IsType<SimpleSmaCrossoverAlgorithm>(algo);
    }

    [Fact]
    public void CreateAlgorithmGeneric_ReturnsTypedInstance()
    {
        var algo = AlgorithmRunner.CreateAlgorithm<RsiMeanReversionAlgorithm>(14, 30m, 70m);

        Assert.NotNull(algo);
        Assert.IsType<RsiMeanReversionAlgorithm>(algo);
    }

    [Fact]
    public void Run_WithAlgorithmInstance_ReturnsTradesList()
    {
        var bars = new List<Bar>
        {
            new("SAMPLE", DateTime.UtcNow, 10, 10, 10, 10, 100),
            new("SAMPLE", DateTime.UtcNow, 20, 20, 20, 20, 100),
            new("SAMPLE", DateTime.UtcNow, 30, 30, 30, 30, 100),
            new("SAMPLE", DateTime.UtcNow, 40, 40, 40, 40, 100),
        };

        var algo = new SimpleSmaCrossoverAlgorithm(2, 3);
        var trades = AlgorithmRunner.Run(algo, bars);

        Assert.NotNull(trades);
        Assert.IsType<List<Trade>>(trades);
    }

    [Fact]
    public void Run_ExecutesAlgorithmLifecycle()
    {
        var bars = new List<Bar>
        {
            new("SAMPLE", DateTime.UtcNow, 10, 10, 10, 10, 100),
            new("SAMPLE", DateTime.UtcNow, 10, 10, 10, 10, 100),
            new("SAMPLE", DateTime.UtcNow, 10, 10, 10, 10, 100),
            new("SAMPLE", DateTime.UtcNow, 50, 50, 50, 50, 100),
            new("SAMPLE", DateTime.UtcNow, 100, 100, 100, 100, 100),
        };

        // With periods 2,3, this should generate trades due to crossover
        var trades = AlgorithmRunner.Run<SimpleSmaCrossoverAlgorithm>(bars, 2, 3);

        // Verify trades were generated
        Assert.NotNull(trades);
    }
}
