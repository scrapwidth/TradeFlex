using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using TradeFlex.Abstractions;
using TradeFlex.BrokerAdapters;
using Alpaca.Markets;

namespace TradeFlex.Cli;

/// <summary>
/// Downloads historical market data from Alpaca.
/// </summary>
public static class DataDownloader
{
    /// <summary>
    /// Downloads historical candle data and saves it as a Parquet file.
    /// </summary>
    public static async Task DownloadAsync(string symbol, DateTime from, DateTime to, string granularity, string outputFile)
    {
        Console.WriteLine($"Downloading {symbol} data from {from:yyyy-MM-dd} to {to:yyyy-MM-dd} via Alpaca...");

        var config = AlpacaConfiguration.FromEnvironment();
        var secretKey = new SecretKey(config.ApiKeyId, config.SecretKey);

        using var client = Environments.Paper.GetAlpacaDataClient(secretKey);

        // Map granularity to Alpaca BarTimeFrame
        var timeFrame = granularity.ToLower() switch
        {
            "1m" => BarTimeFrame.Minute,
            "5m" => new BarTimeFrame(5, BarTimeFrameUnit.Minute),
            "15m" => new BarTimeFrame(15, BarTimeFrameUnit.Minute),
            "1h" => BarTimeFrame.Hour,
            "1d" => BarTimeFrame.Day,
            _ => throw new ArgumentException($"Invalid granularity: {granularity}. Use 1m, 5m, 15m, 1h, or 1d.")
        };

        var bars = new List<Bar>();

        // Use ListHistoricalBarsAsync which handles pagination via the returned IPage
        var request = new HistoricalBarsRequest(symbol, from, to, timeFrame);

        // Fetch all pages
        var page = await client.ListHistoricalBarsAsync(request);

        while (page.Items.Count > 0)
        {
            foreach (var bar in page.Items)
            {
                bars.Add(new Bar(
                    symbol,
                    bar.TimeUtc,
                    bar.Open,
                    bar.High,
                    bar.Low,
                    bar.Close,
                    (long)bar.Volume
                ));
            }

            // Print progress
            Console.Write(".");

            // Get next page
            var token = page.NextPageToken;
            if (token == null) break;

            request = new HistoricalBarsRequest(symbol, from, to, timeFrame) { Pagination = { Token = token } };
            page = await client.ListHistoricalBarsAsync(request);
        }
        Console.WriteLine();

        Console.WriteLine($"Downloaded {bars.Count} bars. Writing to {outputFile}...");

        // Ensure data directory exists
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, outputFile);

        // Write to Parquet
        await WriteParquetAsync(bars, fullPath);

        Console.WriteLine($"Successfully saved to {fullPath}");
    }

    private static async Task WriteParquetAsync(List<Bar> bars, string filePath)
    {
        var schema = new ParquetSchema(
            new DataField<DateTime>("Timestamp"),
            new DataField<decimal>("Open"),
            new DataField<decimal>("High"),
            new DataField<decimal>("Low"),
            new DataField<decimal>("Close"),
            new DataField<long>("Volume"));

        using var fs = File.Create(filePath);
        using var writer = await ParquetWriter.CreateAsync(schema, fs);
        using var group = writer.CreateRowGroup();

        await group.WriteColumnAsync(new DataColumn((DataField<DateTime>)schema[0], bars.Select(b => b.Timestamp).ToArray()));
        await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[1], bars.Select(b => b.Open).ToArray()));
        await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[2], bars.Select(b => b.High).ToArray()));
        await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[3], bars.Select(b => b.Low).ToArray()));
        await group.WriteColumnAsync(new DataColumn((DataField<decimal>)schema[4], bars.Select(b => b.Close).ToArray()));
        await group.WriteColumnAsync(new DataColumn((DataField<long>)schema[5], bars.Select(b => b.Volume).ToArray()));
    }
}
