
namespace GBMS.Models;

public sealed class OwnVehicle
{
    public Position Position { get; set; } = new(0.0, 0.0, 0.0);


    public OwnVehicle()
    {
        Position = new Position(
51.24708,
-2.08829,
325.0);
    }
}
