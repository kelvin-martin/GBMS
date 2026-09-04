
using System.Collections.Generic;
using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Provides persistence of routes to the application's route storage.
/// </summary>
public interface IRoutePersistence
{
    /// <summary>
    /// Loads all valid routes from persistent storage.
    /// Invalid route files are ignored.
    /// </summary>
    IReadOnlyList<Route> LoadAll();

    /// <summary>
    /// Saves a new route to persistent storage.
    /// A new positive route ID is assigned by the persistence service.
    /// </summary>
    void SaveNew(Route route);

    /// <summary>
    /// Saves an existing persisted route.
    /// </summary>
    void Save(Route route);

    /// <summary>
    /// Deletes a persisted route.
    /// </summary>
    void Delete(Route route);
}
