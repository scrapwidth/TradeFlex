using TradeFlex.Core;

namespace TradeFlex.Tests;

public class SimulationClockTests
{
    [Fact]
    public void IdenticalRunsYieldIdenticalTimestamps()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var step = TimeSpan.FromMinutes(1);

        var clock1 = new SimulationClock(start, step);
        var clock2 = new SimulationClock(start, step);

        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(clock1.UtcNow, clock2.UtcNow);
            clock1.Advance();
            clock2.Advance();
        }
    }
}
