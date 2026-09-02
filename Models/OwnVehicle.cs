
using System;
using Avalonia.Logging;
using GBMS.Services;

namespace GBMS.Models;

public sealed class OwnVehicle
{
    public Position Position { get; set; } = new(0.0, 0.0, 0.0);

    // Fixed velocity for the initial movement demonstration. km/h.
    public double Speed { get; } = 30;   // m/s

    public OwnVehicle()
    {
        Position = new Position(51.24708, -2.08829, 90.0);
    }

    /// <summary>
    /// Updates the vehicle position for one simulation step.
    /// </summary>
    /// <param name="simulationStep">Duration of the simulation step.</param>
    public void Update(TimeSpan simulationStep)
    {
        if (simulationStep <= TimeSpan.Zero)
            return;

        // Distance travelled during this simulation step, in metres.
        double distanceMetres =
            Speed * simulationStep.TotalSeconds;

        // Mean Earth radius in metres.
        const double earthRadiusMetres = 6_371_000.0;

        double latitudeRadians =
            Position.Latitude * Math.PI / 180.0;

        double headingRadians =
            Position.Heading * Math.PI / 180.0;

        // Angular distance travelled around the Earth.
        double angularDistance =
            distanceMetres / earthRadiusMetres;

        // Destination latitude.
        double newLatitudeRadians =
            Math.Asin(
                Math.Sin(latitudeRadians) *
                Math.Cos(angularDistance) +
                Math.Cos(latitudeRadians) *
                Math.Sin(angularDistance) *
                Math.Cos(headingRadians));

        // Destination longitude.
        double newLongitudeRadians =
            Position.Longitude * Math.PI / 180.0 +
            Math.Atan2(
                Math.Sin(headingRadians) *
                Math.Sin(angularDistance) *
                Math.Cos(latitudeRadians),
                Math.Cos(angularDistance) -
                Math.Sin(latitudeRadians) *
                Math.Sin(newLatitudeRadians));

        double newLatitude =
            newLatitudeRadians * 180.0 / Math.PI;

        double newLongitude =
            newLongitudeRadians * 180.0 / Math.PI;

        Position = new Position(
            newLatitude, newLongitude, Position.Heading);
    }
}
