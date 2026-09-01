
namespace GBMS.Models;

public enum TacticalEntityAffiliation
{
    Friendly,
    Hostile,
    Neutral,
    Unknown
}

public sealed class TacticalEntity
{
    public TacticalEntityDefinition Definition { get; init; } = null!;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double Heading { get; set; }
}