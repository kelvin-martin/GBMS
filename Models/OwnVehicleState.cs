
using GBMS.Models;

namespace GBMS.Simulation;

/// <summary>
/// Immutable snapshot of the current OwnVehicle simulation state.
/// </summary>
public sealed record OwnVehicleState(Position Position, double Speed);

