using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using TradeFlex.Abstractions;

namespace TradeFlex.Cli;

/// <summary>
/// Downloads historical market data from Coinbase Pro.
/// </summary>
public static class DataDownloader
{
    private static readonly HttpClient _httpClient = new();
    private const string BaseUrl = "https://api.exchange.coinbase.com";

    static DataDownloader()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TradeFlex/1.0");
    }

    /// <summary>
    /// Downloads historical candle data and saves it as a Parquet file.
    /// </summary>
    public static async Task DownloadAsync(string symbol, DateTime from, DateTime to, string granularity, string outputFile)
    {
        Console.WriteLine($"Downloading {symbol} data from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}...");

        var granularitySeconds = ParseGranularity(granularity);
        var bars = new List<Bar>();

        // Coinbase API returns max 300 candles per request
        // Calculate chunk size: 300 candles * granularity seconds
        var maxCandles = 300;
        var chunkDuration = TimeSpan.FromSeconds(granularitySeconds * maxCandles);
        var current = from;

        while (current < to)
        {
            var end = current + chunkDuration;
            if (end > to) end = to;

            Console.WriteLine($"Fetching {current:yyyy-MM-dd HH:mm} to {end:yyyy-MM-dd HH:mm}...");

            try
            {
                var candles = await FetchCandlesAsync(symbol, current, end, granularitySeconds);
                bars.AddRange(candles);
                Console.WriteLine($"  Got {candles.Count} candles");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
                Console.WriteLine($"  Skipping this chunk and continuing...");
            }

            current = end;

            // Rate limit: 10 req/sec, so wait 150ms between requests to be safe
            await Task.Delay(150);
        }

        Console.WriteLine($"Downloaded {bars.Count} bars. Writing to {outputFile}...");

        // Ensure data directory exists
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        var fullPath = Path.Combine(dataDir, outputFile);

        // Write to Parquet
        await WriteParquetAsync(bars, fullPath);

        Console.WriteLine($"Successfully saved to {fullPath}");
    }

    private static async Task<List<Bar>> FetchCandlesAsync(string symbol, DateTime start, DateTime end, int granularity)
    {
        var startIso = start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var endIso = end.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        
        var url = $"{BaseUrl}/products/{symbol}/candles?start={startIso}&end={endIso}&granularity={granularity}";
        
        Console.WriteLine($"  API URL: {url}");
        
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"  API Error Response: {errorBody}");
            response.EnsureSuccessStatusCode();
        }
        
        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<List<List<JsonElement>>>(json);

        if (data == null) return new List<Bar>();

        var bars = new List<Bar>();
        foreach (var candle in data)
        {
            // Coinbase format: [timestamp, low, high, open, close, volume]
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(candle[0].GetInt64()).UtcDateTime;
            var low = candle[1].GetDecimal();
            var high = candle[2].GetDecimal();
            var open = candle[3].GetDecimal();
            var close = candle[4].GetDecimal();
            var volume = (long)candle[5].GetDecimal();

            bars.Add(new Bar(symbol, timestamp, open, high, low, close, volume));
        }

        // Coinbase returns newest first, so reverse to get chronological order
        bars.Reverse();
        return bars;
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

    private static int ParseGranularity(string granularity)
    {
        return granularity.ToLower() switch
        {
            "1m" => 60,
            "5m" => 300,
            "15m" => 900,
            "1h" => 3600,
            "6h" => 21600,
            "1d" => 86400,
            _ => throw new ArgumentException($"Invalid granularity: {granularity}. Use 1m, 5m, 15m, 1h, 6h, or 1d.")
        };
    }
}
