using System.Collections.Generic;
using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Loads vehicle type definitions from the application's vehicle type
/// library. Read-only - vehicle types are hand-edited configuration, not
/// data the running application creates, modifies, or deletes.
/// </summary>
public interface IVehicleTypePersistence
{
    /// <summary>
    /// Loads all valid vehicle types from the vehicle type library.
    /// Invalid files are ignored.
    /// </summary>
    IReadOnlyList<VehicleType> LoadAll();
}