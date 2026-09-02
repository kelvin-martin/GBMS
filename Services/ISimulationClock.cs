

using System;

namespace GBMS.Services;

public interface ISimulationClock
{
    DateTimeOffset UtcNow { get; }

    long Timestamp { get; }

    TimeSpan Elapsed { get; }
}
