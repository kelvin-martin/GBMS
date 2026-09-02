
using System;

namespace GBMS.Simulation;

public static class SimulationFactory
{
    private static SimulationManager? _instance;

    public static SimulationManager Create()
    {
        if (_instance != null)
        {
            throw new InvalidOperationException(
                "SimulationManager has already been created.");
        }

        _instance = new SimulationManager();

        return _instance;
    }

    public static SimulationManager Current =>
        _instance ?? throw new InvalidOperationException(
            "SimulationManager has not been created.");
}
