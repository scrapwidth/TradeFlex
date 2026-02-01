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
            new Bar("SAMPLE", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1.0m, 1.1m, 0.9m, 1.05m, 1000),
            new Bar("SAMPLE", new DateTime(2024, 1, 1, 0, 1, 0, DateTimeKind.Utc), 1.05m, 1.15m, 1.0m, 1.1m, 1500),
            new Bar("SAMPLE", new DateTime(2024, 1, 1, 0, 2, 0, DateTimeKind.Utc), 1.1m, 1.2m, 1.05m, 1.15m, 1600)
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
        await foreach (var bar in ParquetBarDataLoader.LoadAsync("minute_fixture.parquet", "SAMPLE"))
        {
            bars.Add(bar);
        }

        Assert.Equal(3, bars.Count);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), bars[0].Timestamp);
    }

    [Fact]
    public async Task LoadsBarsVerifiesAllFields()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        var filePath = Path.Combine(dataDir, "fields_fixture.parquet");

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

            await group.WriteColumnAsync(new DataColumn((DataField<DateTime>)schema[0],
                new[] { new DateTime(2024, 1, 1, 12, 30, 0, DateTimeKind.Utc) }));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[1], new decimal[] { 100.5m }));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[2], new decimal[] { 105.25m }));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[3], new decimal[] { 99.75m }));
            await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[4], new decimal[] { 103.50m }));
            await group.WriteColumnAsync(new DataColumn((DataField<long>)schema[5], new long[] { 5000 }));
        }

        var bars = new List<Bar>();
        await foreach (var bar in ParquetBarDataLoader.LoadAsync("fields_fixture.parquet", "TEST"))
        {
            bars.Add(bar);
        }

        Assert.Single(bars);
        var b = bars[0];
        Assert.Equal("TEST", b.Symbol);
        Assert.Equal(100.5m, b.Open);
        Assert.Equal(105.25m, b.High);
        Assert.Equal(99.75m, b.Low);
        Assert.Equal(103.50m, b.Close);
        Assert.Equal(5000, b.Volume);
    }
}
