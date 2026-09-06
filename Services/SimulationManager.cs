
using System;
using System.Threading;
using System.Threading.Tasks;
using GBMS.Models;

namespace GBMS.Services;

public class SimulationManager
{
    private readonly SimulationLoop _simulationLoop;
    private readonly RouteManager _routeManager;

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

        _routeManager = ApplicationFactory.RouteManager;

        if (_routeManager == null)
            throw new InvalidOperationException("RouteManager is not initialized.");

        _routeManager.AssignedRouteChanged += OnAssignedRouteChanged;
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

    /// <summary>
    /// Forwards route assignment changes from the Route Manager to Own Vehicle.
    /// </summary>
    /// <param name="route">The newly assigned route, or <c>null</c> if none is assigned.</param>
    private void OnAssignedRouteChanged(Route? route)
    {
        OwnVehicle.AssignRoute(route);
    }
}
