using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TradeFlex.Abstractions;

/// <summary>
/// Represents a source of market data bars.
/// </summary>
public interface IMarketDataFeed
{
    /// <summary>
    /// Gets a stream of bars.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the stream.</param>
    /// <returns>An async enumerable of bars.</returns>
    IAsyncEnumerable<Bar> GetFeedAsync(CancellationToken cancellationToken = default);
}
