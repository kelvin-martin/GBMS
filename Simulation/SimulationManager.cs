
using System;
using System.Threading;
using System.Threading.Tasks;
using GBMS.Models;
using GBMS.Services;

namespace GBMS.Simulation;

public class SimulationManager
{
    private readonly SimulationLoop _simulationLoop;

    private CancellationTokenSource? _simulationCancellation;
    private Task? _simulationTask;

    public OwnVehicle OwnVehicle { get; }

    public ISimulationClock Clock { get; }

    public SimulationState SimulationState { get; }


    public SimulationManager()
    {
        Clock = new SimulationClock();

        OwnVehicle = new OwnVehicle();

        SimulationState = new SimulationState();

        _simulationLoop = new SimulationLoop(Clock, 10.0);
    }

    public void Start()
    {
        if (_simulationTask != null)
            return;

        _simulationCancellation = new CancellationTokenSource();

        _simulationTask = Task.Run(
            () => _simulationLoop.Run(
                UpdateSimulation,  _simulationCancellation.Token));
    }

    public void Stop()
    {
        if (_simulationTask == null)
            return;

        _simulationCancellation?.Cancel();

        _simulationTask.Wait();

        _simulationTask = null;

        _simulationCancellation?.Dispose();
        _simulationCancellation = null;
    }

    private void UpdateSimulation(DateTimeOffset simulationTime, TimeSpan simulationStep)
    {
        OwnVehicle.Update(simulationStep);

        SimulationState.UpdateOwnVehicle(OwnVehicle);
    }
}
