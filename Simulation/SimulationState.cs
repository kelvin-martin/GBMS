using System.Threading;
using GBMS.Models;

namespace GBMS.Simulation;

/// <summary>
/// Represents the current state of the simulation.
/// </summary>
public sealed class SimulationState
{
    private OwnVehicleState? _ownVehicle;

    public OwnVehicleState? OwnVehicle => Volatile.Read(ref _ownVehicle);

    public void UpdateOwnVehicle(OwnVehicle vehicle)
    {
        var state = new OwnVehicleState(vehicle.Position, vehicle.Speed);

        Volatile.Write(ref _ownVehicle, state);
    }
}