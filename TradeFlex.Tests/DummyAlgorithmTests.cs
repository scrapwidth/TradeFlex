using System.Threading.Tasks;
using TradeFlex.Abstractions;

namespace TradeFlex.Tests;

public class DummyAlgorithmTests
{
    private sealed class DummyAlgorithm : ITradingAlgorithm
    {
        public Task InitializeAsync(IAlgorithmContext context) => Task.CompletedTask;

        public Task OnBarAsync(Bar bar) => Task.CompletedTask;

        public Task OnExitAsync() => Task.CompletedTask;

        public bool OnRiskCheck(Order order) => true;
    }

    [Fact]
    public void DummyAlgorithmLoads()
    {
        ITradingAlgorithm algo = new DummyAlgorithm();
        Assert.NotNull(algo);
    }
}
