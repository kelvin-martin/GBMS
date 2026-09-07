namespace GBMS.Models;

/// <summary>
/// Describes the physical capabilities of an armoured vehicle type,
/// independent of any particular simulation run. See
/// GBMS_Vehicle_Configuration_Design.md.
/// </summary>
public class VehicleType
{
    public string VehicleTypeId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Minimum speed, in km/h.</summary>
    public double MinSpeedKmh { get; set; }

    /// <summary>Maximum speed, in km/h.</summary>
    public double MaxSpeedKmh { get; set; }

    public double MaxTurnRateDegreesPerSecond { get; set; }

    // Not yet used by any navigation logic - see the design doc §3/§6.
    public double MinAccelerationMetresPerSecondSquared { get; set; }
    public double MaxAccelerationMetresPerSecondSquared { get; set; }
}
