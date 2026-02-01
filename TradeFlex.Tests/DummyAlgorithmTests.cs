using TradeFlex.Abstractions;

namespace TradeFlex.Tests;

public class DummyAlgorithmTests
{
    private sealed class DummyAlgorithm : ITradingAlgorithm
    {
        public void Initialize(IAlgorithmContext context) { }

        public void OnBar(Bar bar) { }

        public void OnExit() { }

        public bool OnRiskCheck(Order order) => true;
    }

    [Fact]
    public void DummyAlgorithmLoads()
    {
        ITradingAlgorithm algo = new DummyAlgorithm();
        Assert.NotNull(algo);
    }
}
