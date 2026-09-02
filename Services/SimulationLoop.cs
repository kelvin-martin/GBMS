using System;
using System.Threading;

namespace GBMS.Services;

/// <summary>
/// Manages the execution of a simulation loop based on a specified clock and frequency.
/// </summary>
/// <remarks>This class is sealed and cannot be inherited.</remarks>
public sealed class SimulationLoop
{
    private readonly ISimulationClock _clock;
    private readonly TimeSpan _simulationStep;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationLoop"/> class with the specified clock and frequency.
    /// </summary>
    /// <param name="clock">The simulation clock to use for timing.</param>
    /// <param name="frequencyHz">The target frequency of the simulation loop in hertz.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="frequencyHz"/> is less than or equal to zero.</exception>
    public SimulationLoop(
        ISimulationClock clock,
        double frequencyHz)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (frequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequencyHz));

        _clock = clock;
        _simulationStep =
            TimeSpan.FromSeconds(1.0 / frequencyHz);
    }

    /// <summary>
    /// Executes a simulation loop, invoking the update action at regular intervals until cancellation is requested.    
    /// </summary>
    /// <param name="update">The action to invoke with the current simulation time and step duration.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public void Run(Action<DateTimeOffset, TimeSpan> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        var simulationStartUtc = _clock.UtcNow;
        var simulationTime = TimeSpan.Zero;

        var nextTick = _clock.Elapsed + _simulationStep;

        while (!cancellationToken.IsCancellationRequested)
        {
            while (_clock.Elapsed < nextTick)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                Thread.Yield();
            }

            simulationTime += _simulationStep;

            var simulationUtc = simulationStartUtc + simulationTime;

            update(simulationUtc, _simulationStep);

            nextTick += _simulationStep;
        }
    }

}