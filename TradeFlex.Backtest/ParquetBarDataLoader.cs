using Parquet;
using Parquet.Data;
using System.Collections;
using System.Linq;
using TradeFlex.Abstractions;

namespace TradeFlex.Backtest;

/// <summary>
/// Loads <see cref="Bar"/> records from minute-bar Parquet files.
/// </summary>
public static class ParquetBarDataLoader
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

    /// <summary>
    /// Asynchronously reads bars from a Parquet file under the data directory.
    /// </summary>
    /// <param name="fileName">The file name within the <c>/data</c> directory.</param>
    /// <param name="symbol">The symbol to assign to the loaded bars.</param>
    /// <returns>An async enumerable of bars.</returns>
    public static async IAsyncEnumerable<Bar> LoadAsync(string fileName, string symbol)
    {
        var path = Path.Combine(DataDirectory, fileName);

        using var stream = File.OpenRead(path);
        using var reader = await ParquetReader.CreateAsync(stream);
        var dataFields = reader.Schema.GetDataFields();

        for (int g = 0; g < reader.RowGroupCount; g++)
        {
            using var groupReader = reader.OpenRowGroupReader(g);
            var columns = new DataColumn[dataFields.Length];
            for (int i = 0; i < dataFields.Length; i++)
            {
                columns[i] = await groupReader.ReadColumnAsync(dataFields[i]);
            }

            var timestamps = ((IEnumerable)columns[0].Data).Cast<object>()
                .Select(v => v switch
                {
                    DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                    DateTimeOffset dto => dto.UtcDateTime,
                    _ => DateTime.Parse(v.ToString()!)
                }).ToArray();
            var opens = ((IEnumerable)columns[1].Data).Cast<object>().Select(v => Convert.ToDecimal(v)).ToArray();
            var highs = ((IEnumerable)columns[2].Data).Cast<object>().Select(v => Convert.ToDecimal(v)).ToArray();
            var lows = ((IEnumerable)columns[3].Data).Cast<object>().Select(v => Convert.ToDecimal(v)).ToArray();
            var closes = ((IEnumerable)columns[4].Data).Cast<object>().Select(v => Convert.ToDecimal(v)).ToArray();
            var volumes = ((IEnumerable)columns[5].Data).Cast<object>().Select(v => Convert.ToInt64(v)).ToArray();

            for (int i = 0; i < timestamps.Length; i++)
            {
                yield return new Bar(symbol, timestamps[i], opens[i], highs[i], lows[i], closes[i], volumes[i]);
            }
        }
    }
}

