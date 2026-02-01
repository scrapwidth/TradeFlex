using System;
using System.IO;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System.Linq;
using TradeFlex.Abstractions;
using TradeFlex.Backtest;
using TradeFlex.Core;
using Xunit;
using System.Threading.Tasks;

namespace TradeFlex.Tests;

public class BacktestEngineTests
{
    private sealed class CountingAlgorithm : BaseAlgorithm
    {
        public int Count { get; private set; }
        public override void OnBar(Bar bar) => Count++;
    }

    [Fact]
    public async Task EngineRunsThroughBars()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        var file = Path.Combine(dataDir, "engine_fixture.parquet");

        var bars = new[]
        {
            new Bar("SAMPLE", new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc),1,1,1,1,1),
            new Bar("SAMPLE", new DateTime(2024,1,1,0,1,0,DateTimeKind.Utc),1,1,1,1,1),
        };

        var schema = new Parquet.Schema.ParquetSchema(
            new DataField<DateTime>("Timestamp"),
            new DataField<decimal>("Open"),
            new DataField<decimal>("High"),
            new DataField<decimal>("Low"),
            new DataField<decimal>("Close"),
            new DataField<long>("Volume"));

        await using (var fs = File.Create(file))
        {
            using var writer = await Parquet.ParquetWriter.CreateAsync(schema, fs);
            using var group = writer.CreateRowGroup();
            await group.WriteColumnAsync(new Parquet.Data.DataColumn((DataField<DateTime>)schema[0], bars.Select(b => b.Timestamp).ToArray()));
            await group.WriteColumnAsync(new Parquet.Data.DataColumn((DataField<decimal>)schema[1], bars.Select(b => b.Open).ToArray()));
            await group.WriteColumnAsync(new Parquet.Data.DataColumn((DataField<decimal>)schema[2], bars.Select(b => b.High).ToArray()));
            await group.WriteColumnAsync(new Parquet.Data.DataColumn((DataField<decimal>)schema[3], bars.Select(b => b.Low).ToArray()));
            await group.WriteColumnAsync(new Parquet.Data.DataColumn((DataField<decimal>)schema[4], bars.Select(b => b.Close).ToArray()));
            await group.WriteColumnAsync(new Parquet.Data.DataColumn((DataField<long>)schema[5], bars.Select(b => b.Volume).ToArray()));
        }

        var algo = new CountingAlgorithm();
        var clock = new SimulationClock(bars[0].Timestamp, TimeSpan.FromMinutes(1));
        var engine = new BacktestEngine(clock);
        var trades = await engine.RunAsync(algo, "engine_fixture.parquet", "SAMPLE");

        Assert.Equal(2, algo.Count);
        Assert.Empty(trades);
    }
}
