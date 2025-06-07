using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System.Linq;
using TradeFlex.Backtest;
using TradeFlex.Abstractions;

namespace TradeFlex.Tests;

public class ParquetBarDataLoaderTests
{
    [Fact]
    public async Task LoadsBarsFromParquet()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        var filePath = Path.Combine(dataDir, "minute_fixture.parquet");

        var fixtureBars = new[]
        {
            new Bar(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1.0m, 1.1m, 0.9m, 1.05m, 1000),
            new Bar(new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), 1.05m, 1.15m, 1.0m, 1.1m, 1500),
            new Bar(new DateTime(2024, 1, 1, 0, 2, 0, DateTimeKind.Utc), 1.1m, 1.2m, 1.05m, 1.15m, 1600)
        };

        var schema = new ParquetSchema(
            new DataField<DateTime>("Timestamp"),
            new DataField<decimal>("Open"),
            new DataField<decimal>("High"),
            new DataField<decimal>("Low"),
            new DataField<decimal>("Close"),
            new DataField<long>("Volume"));

        await using (var fs = File.Create(filePath))
        {
            using var writer = await ParquetWriter.CreateAsync(schema, fs);
            using var group = writer.CreateRowGroup();

            await group.WriteColumnAsync(new DataColumn((DataField<DateTime>)schema[0], fixtureBars.Select(b => b.Timestamp).ToArray()));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[1], fixtureBars.Select(b => b.Open).ToArray()));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[2], fixtureBars.Select(b => b.High).ToArray()));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[3], fixtureBars.Select(b => b.Low).ToArray()));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[4], fixtureBars.Select(b => b.Close).ToArray()));
            await group.WriteColumnAsync(new DataColumn((DataField<long>)schema[5], fixtureBars.Select(b => b.Volume).ToArray()));
        }

        var bars = new List<Bar>();
        await foreach (var bar in ParquetBarDataLoader.LoadAsync("minute_fixture.parquet"))
        {
            bars.Add(bar);
        }

        Assert.Equal(3, bars.Count);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), bars[0].Timestamp);
    }
}
