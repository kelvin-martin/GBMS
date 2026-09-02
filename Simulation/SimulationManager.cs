
using GBMS.Models;
using GBMS.Services;

namespace GBMS.Simulation;

public class SimulationManager
{
    public OwnVehicle OwnVehicle { get; }

    public ISimulationClock Clock { get; }

    public SimulationManager()
    {
        Clock = new SimulationClock();
        OwnVehicle = new OwnVehicle();
    }
}
