
namespace GBMS.Models;

public enum TacticalEntityAffiliation
{
    Friendly,
    Hostile,
    Neutral,
    Unknown
}

/// <summary>
/// Represents a tactical track on the map.
/// Data describing a tactical entity that is to be represented on the map.
/// </summary>
public sealed class TacticalTrack
{
    public TacticalEntityDefinition Definition { get; init; } = null!;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double Heading { get; set; }
}