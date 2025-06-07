using System;

namespace TradeFlex.Core;

/// <summary>
/// Provides deterministic time progression for simulations and tests.
/// </summary>
public sealed class SimulationClock
{
    private readonly TimeSpan _step;
    private DateTime _current;

    /// <summary>
    /// Initializes the clock at a specific start time and step increment.
    /// </summary>
    /// <param name="start">The first timestamp to return.</param>
    /// <param name="step">How much time passes on each <see cref="Advance"/> call.</param>
    public SimulationClock(DateTime start, TimeSpan step)
    {
        _current = start;
        _step = step;
    }

    /// <summary>
    /// Gets the current simulated time.
    /// </summary>
    public DateTime UtcNow => _current;

    /// <summary>
    /// Advances time by the configured step and returns the new value.
    /// </summary>
    /// <returns>The advanced timestamp.</returns>
    public DateTime Advance()
    {
        _current = _current.Add(_step);
        return _current;
    }
}

