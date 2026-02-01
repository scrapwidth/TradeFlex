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
}
