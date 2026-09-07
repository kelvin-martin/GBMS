namespace GBMS.Models;

/// <summary>
/// Describes what is active for a given simulation run: which vehicle
/// type Own Vehicle is, and its starting position/heading. See
/// GBMS_Vehicle_Configuration_Design.md.
/// </summary>
public class ScenarioConfiguration
{
    public string VehicleTypeId { get; set; } = string.Empty;

    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double StartHeading { get; set; }
}