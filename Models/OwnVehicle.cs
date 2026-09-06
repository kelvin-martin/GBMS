
using System;
using GBMS.Services;

namespace GBMS.Models;

public sealed class OwnVehicle
{
    private Route? _assignedRoute;
    private int _currentWaypointIndex;

    public Position Position { get; set; } = new(0.0, 0.0, 0.0);

    // Fixed velocity for the placeholder straight-bearing movement. Metres/second.
    public double Speed { get; } = 30;   // m/s

    /// <summary>
    /// Maximum rate at which Own Vehicle can change heading while navigating,
    /// in degrees per second. Configurable - defaults to a mid-range value
    /// (10-20°/s discussed) pending a real vehicle specification.
    /// </summary>
    public double MaxTurnRateDegreesPerSecond { get; set; } = 15.0;

    public OwnVehicle()
    {
        Position = new Position(51.24708, -2.08829, 90.0);
    }

    /// <summary>
    /// Assigns a route for Own Vehicle to navigate, resetting progress to the
    /// first waypoint. Passing <c>null</c> clears the assignment, reverting to
    /// placeholder straight-bearing motion.
    /// </summary>
    /// <param name="route">The route to navigate, or <c>null</c> to clear the assignment.</param>
    public void AssignRoute(Route? route)
    {
        _assignedRoute = route;
        _currentWaypointIndex = 0;
    }

    /// <summary>
    /// Updates the vehicle position for one simulation step.
    /// </summary>
    /// <param name="simulationStep">Duration of the simulation step.</param>
    public void Update(TimeSpan simulationStep)
    {
        if (simulationStep <= TimeSpan.Zero)
            return;

        if (_assignedRoute == null)
        {
            UpdatePlaceholder(simulationStep);
            return;
        }

        if (_currentWaypointIndex >= _assignedRoute.Waypoints.Count)
        {
            // All waypoints reached - hold position and heading.
            // (Not the same as the unassigned case: falling through to
            // placeholder motion here would drive the vehicle straight past
            // the final waypoint instead of stopping.)
            return;
        }

        UpdateNavigating(simulationStep);
    }

    /// <summary>
    /// Moves the vehicle toward its current target waypoint, advancing to the
    /// next waypoint on arrival.
    /// </summary>
    private void UpdateNavigating(TimeSpan simulationStep)
    {
        Waypoint target = _assignedRoute!.Waypoints[_currentWaypointIndex];

        double distanceToTargetMetres = Mapping.CalculateDistanceKm(
            Position.Latitude, Position.Longitude,
            target.Latitude, target.Longitude) * 1000.0;

        double desiredBearingDegrees = BearingDegrees(
            Position.Latitude, Position.Longitude,
            target.Latitude, target.Longitude);

        double maxTurnThisTickDegrees =
            MaxTurnRateDegreesPerSecond * simulationStep.TotalSeconds;

        double headingDegrees = ApplyTurnRateLimit(
            Position.Heading, desiredBearingDegrees, maxTurnThisTickDegrees);

        // Waypoint speed is km/h; vehicle motion here works in metres/second.
        double speedMetresPerSecond = target.Speed / 3.6;

        double travelDistanceMetres =
            speedMetresPerSecond * simulationStep.TotalSeconds;

        if (distanceToTargetMetres <= travelDistanceMetres)
        {
            // Arrived. Snap position to the waypoint. Heading still respects
            // the turn-rate limit rather than snapping to face the waypoint
            // exactly - the vehicle finishes turning onto the next leg over
            // subsequent ticks, same as any other heading change.
            Position = new Position(
                target.Latitude, target.Longitude, headingDegrees);

            _currentWaypointIndex++;

            return;
        }

        var (newLatitude, newLongitude) = Mapping.ProjectPosition(
            Position.Latitude, Position.Longitude,
            headingDegrees, travelDistanceMetres / 1000.0);

        Position = new Position(newLatitude, newLongitude, headingDegrees);
    }

    /// <summary>
    /// Existing placeholder motion: straight bearing at fixed speed. Unchanged
    /// from the original behaviour - used whenever no route is assigned.
    /// </summary>
    private void UpdatePlaceholder(TimeSpan simulationStep)
    {
        double distanceMetres =
            Speed * simulationStep.TotalSeconds;

        var (newLatitude, newLongitude) = Mapping.ProjectPosition(
            Position.Latitude, Position.Longitude,
            Position.Heading, distanceMetres / 1000.0);

        Position = new Position(newLatitude, newLongitude, Position.Heading);
    }

    /// <summary>
    /// Turns the current heading toward the desired heading, limited to the
    /// maximum angular change allowed for this tick. This is what prevents
    /// the vehicle's heading from snapping instantly to face a waypoint.
    /// </summary>
    private static double ApplyTurnRateLimit(
        double currentHeadingDegrees, double desiredHeadingDegrees, double maxTurnDegrees)
    {
        double delta = NormalizeAngleDifference(
            desiredHeadingDegrees - currentHeadingDegrees);

        double clampedDelta = Math.Clamp(delta, -maxTurnDegrees, maxTurnDegrees);

        return NormalizeDegrees(currentHeadingDegrees + clampedDelta);
    }

    /// <summary>
    /// Normalizes an angular difference to the range (-180, 180] degrees, so
    /// turning always takes the shorter direction around the compass.
    /// </summary>
    private static double NormalizeAngleDifference(double degrees)
    {
        double normalized = degrees % 360.0;

        if (normalized > 180.0) normalized -= 360.0;
        if (normalized <= -180.0) normalized += 360.0;

        return normalized;
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 360) degrees.
    /// </summary>
    private static double NormalizeDegrees(double degrees)
    {
        return ((degrees % 360.0) + 360.0) % 360.0;
    }

    /// <summary>
    /// Initial great-circle bearing from point 1 to point 2, in degrees (0-360).
    /// Not currently duplicated elsewhere in the codebase, so kept local here
    /// rather than promoted to a shared utility.
    /// </summary>
    private static double BearingDegrees(
        double lat1, double lon1, double lat2, double lon2)
    {
        double lat1Rad = lat1 * Math.PI / 180.0;
        double lat2Rad = lat2 * Math.PI / 180.0;
        double deltaLonRad = (lon2 - lon1) * Math.PI / 180.0;

        double y = Math.Sin(deltaLonRad) * Math.Cos(lat2Rad);
        double x =
            Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
            Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(deltaLonRad);

        double bearingDegrees =
            Math.Atan2(y, x) * 180.0 / Math.PI;

        return (bearingDegrees + 360.0) % 360.0;
    }
}
