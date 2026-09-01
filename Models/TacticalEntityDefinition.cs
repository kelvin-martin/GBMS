
namespace GBMS.Models;

public enum TacticalEntityType
{
    GroundVehicle,
    GroundEquipment,
    C2Unit
}

public sealed class TacticalEntityDefinition
{
    public TacticalEntityType EntityType { get; init; }

    public TacticalEntityAffiliation Affiliation { get; init; }

    public string SymbolId { get; init; } = string.Empty;
}
