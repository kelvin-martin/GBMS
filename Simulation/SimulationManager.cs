
using GBMS.Models;

namespace GBMS.Simulation;

public class SimulationManager
{
    public OwnVehicle OwnVehicle { get; }

    public SimulationManager()
    {
        OwnVehicle = new OwnVehicle();
    }
}
