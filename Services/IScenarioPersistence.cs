using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Loads the scenario configuration selecting what is active for the
/// current simulation run. Read-only - the scenario file is hand-edited
/// configuration, not data the running application creates or modifies.
/// </summary>
public interface IScenarioPersistence
{
    /// <summary>
    /// Loads the scenario configuration, or <c>null</c> if the file is
    /// missing or invalid.
    /// </summary>
    ScenarioConfiguration? Load();
}