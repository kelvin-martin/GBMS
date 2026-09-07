
using System;
using GBMS.Services;

namespace GBMS.Models;

public sealed class OwnVehicle
{
    private Route? _assignedRoute;
    private Waypoint? _currentTargetWaypoint;

    private readonly double _minSpeedMetresPerSecond;
    private readonly double _maxSpeedMetresPerSecond;

    public Position Position { get; set; }

    /// <summary>
    /// Current vehicle speed, in metres/second. Zero while stationary (no
    /// route assigned, or the assigned route has been fully navigated).
    /// While navigating, set each tick to the current target waypoint's
    /// speed, converted from km/h and clamped to this vehicle's
    /// configured [MinSpeedKmh, MaxSpeedKmh].
    /// </summary>
    public double Speed { get; private set; } = 0.0;

    /// <summary>
    /// Maximum rate at which Own Vehicle can change heading while
    /// navigating, in degrees per second. Fixed by vehicle configuration -
    /// see GBMS_Vehicle_Configuration_Design.md §6.
    /// </summary>
    public double MaxTurnRateDegreesPerSecond { get; }

    /// <summary>
    /// Constructs Own Vehicle from a resolved vehicle type and starting
    /// scenario state.
    /// </summary>
    /// <param name="vehicleType">The resolved vehicle type capabilities.</param>
    /// <param name="scenario">The starting position/heading for this run.</param>
    public OwnVehicle(VehicleType vehicleType, ScenarioConfiguration scenario)
    {
        ArgumentNullException.ThrowIfNull(vehicleType);
        ArgumentNullException.ThrowIfNull(scenario);

        Position = new Position(
            scenario.StartLatitude, scenario.StartLongitude, scenario.StartHeading);

        _minSpeedMetresPerSecond = vehicleType.MinSpeedKmh / 3.6;
        _maxSpeedMetresPerSecond = vehicleType.MaxSpeedKmh / 3.6;

        MaxTurnRateDegreesPerSecond = vehicleType.MaxTurnRateDegreesPerSecond;
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
        _currentTargetWaypoint = route?.Waypoints.Count > 0
            ? route.Waypoints[0]
            : null;
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
            // No route assigned - Own Vehicle remains stationary until one is.
            // This replaces the previous straight-bearing placeholder motion.
            Speed = 0.0;
            return;
        }

        if (_currentTargetWaypoint == null ||
            !_assignedRoute.Waypoints.Contains(_currentTargetWaypoint))
        {
            // Target reached the end, or was deleted out from under
            // navigation - hold position and heading rather than guessing.
            Speed = 0.0;
            return;
        }

        UpdateNavigating(simulationStep);

        /***** Debug logging is disabled by default to avoid excessive log volume. Enable if needed for troubleshooting. *****
        Logger.Debug(
            "Own Vehicle updated: Position ({Lat}, {Lon}), Heading {Heading}, Speed {Speed} m/s, Target Waypoint {TargetLat}, {TargetLon}",
            Position.Latitude, Position.Longitude, Position.Heading, Speed,
            _currentTargetWaypoint.Latitude, _currentTargetWaypoint.Longitude);
        *****/
    }


    /// <summary>
    /// Moves the vehicle toward its current target waypoint, advancing to the
    /// next waypoint on arrival.
    /// </summary>
    private void UpdateNavigating(TimeSpan simulationStep)
    {
        Waypoint target = _currentTargetWaypoint!;

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

        // Waypoint speed is km/h; vehicle motion works in metres/second. The
        // waypoint expresses a desired speed, not a guarantee - it is clamped
        // to this vehicle's own configured limits, since the same route may
        // be assigned to a different vehicle type with different capabilities
        // on another run. See GBMS_Vehicle_Configuration_Design.md §6.
        double requestedSpeedMetresPerSecond = target.Speed / 3.6;

        Speed = Math.Clamp(
            requestedSpeedMetresPerSecond,
            _minSpeedMetresPerSecond,
            _maxSpeedMetresPerSecond);

        double travelDistanceMetres = Speed * simulationStep.TotalSeconds;

        if (distanceToTargetMetres <= travelDistanceMetres)
        {
            int targetIndex = _assignedRoute!.Waypoints.IndexOf(target);
            int nextIndex = targetIndex + 1;

            _currentTargetWaypoint = nextIndex < _assignedRoute.Waypoints.Count
                ? _assignedRoute.Waypoints[nextIndex]
                : null;

            return;
        }

        var (newLatitude, newLongitude) = Mapping.ProjectPosition(
            Position.Latitude, Position.Longitude,
            headingDegrees, travelDistanceMetres / 1000.0);

        Position = new Position(newLatitude, newLongitude, headingDegrees);
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
