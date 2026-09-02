
using System;
using System.Diagnostics;

namespace GBMS.Services;

/// <summary>
/// Represents a high-resolution simulation clock synchronized to UTC.
/// </summary>
public sealed class SimulationClock : ISimulationClock
{
    private readonly long _startTimestamp;

    private readonly DateTimeOffset _startUtc;

    private static readonly double TimestampFrequency = (double)Stopwatch.Frequency;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationClock"/> class.
    /// </summary>
    public SimulationClock()
    {
        _startTimestamp = Stopwatch.GetTimestamp();
        _startUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Current simulation time synchronised to UTC at clock creation.
    /// </summary>
    public DateTimeOffset UtcNow
    {
        get
        {
            var elapsed = GetElapsedTime();
            return _startUtc + elapsed;
        }
    }

    /// <summary>
    /// Elapsed time since the simulation clock was created.
    /// </summary>
    public TimeSpan Elapsed => GetElapsedTime();

    /// <summary>
    /// Current high-resolution timestamp.
    /// </summary>
    public long Timestamp => Stopwatch.GetTimestamp();

    /// <summary>
    /// Gets the elapsed time since the simulation clock was created.
    /// </summary>
    /// <returns>The elapsed <see cref="TimeSpan"/>.</returns>
    private TimeSpan GetElapsedTime()
    {
        long currentTimestamp = Stopwatch.GetTimestamp();
        long elapsedTicks = currentTimestamp - _startTimestamp;

        return TimeSpan.FromSeconds(elapsedTicks / TimestampFrequency);
    }
}