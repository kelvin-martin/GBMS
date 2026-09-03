using System;
using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Manages the temporary route being created or edited on the Tactical Map.
/// </summary>
public sealed class RouteEditor
{
    public event Action<bool>? CreationModeChanged;

    /// <summary>
    /// Gets the route currently being edited.
    /// </summary>
    public Route? CurrentRoute { get; private set; }

    /// <summary>
    /// Gets a value indicating whether Route Creation Mode is active.
    /// </summary>
    public bool IsCreationMode { get; private set; }

    // Waypoint speed limits for the Tactical Map route editor.
    private const int DefaultWaypointSpeed = 30;
    private const int MinimumWaypointSpeed = 10;
    private const int MaximumWaypointSpeed = 70;

    private int? _draggedWaypointIndex;

    public int? DraggedWaypointIndex => _draggedWaypointIndex;

    private int? _selectedWaypointIndex;
    private int? _speedEditOriginalSpeed;

    public int? SelectedWaypointIndex => _selectedWaypointIndex;

    public bool IsSpeedEditActive =>
        _selectedWaypointIndex != null &&
        _speedEditOriginalSpeed != null;

    /// <summary>
    /// Toggles Route Creation Mode.
    /// </summary>
    public void ToggleCreationMode()
    {
        if (IsCreationMode)
        {
            IsCreationMode = false;
            CreationModeChanged?.Invoke(IsCreationMode);
            return;
        }

        StartRouteCreation();
    }

    /// <summary>
    /// Starts Route Creation Mode.
    /// </summary>
    /// <remarks>
    /// If a temporary route already exists, it is retained and editing
    /// resumes on that route rather than creating a new route.
    /// </remarks>
    public void StartRouteCreation()
    {
        if (CurrentRoute == null)
        {
            CurrentRoute = new Route
            {
                Id = Guid.NewGuid(),
                Name = "Temporary Route",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            };
        }

        IsCreationMode = true;
        CreationModeChanged?.Invoke(IsCreationMode);
    }

    /// <summary>
    /// Stops Route Creation Mode without discarding the current route.
    /// </summary>
    public void StopRouteCreation()
    {
        IsCreationMode = false;
        CreationModeChanged?.Invoke(IsCreationMode);
    }


    public bool BeginWaypointDrag(int index)
    {
        if (CurrentRoute == null)
            return false;

        if (index < 0 || index >= CurrentRoute.Waypoints.Count)
            return false;

        _draggedWaypointIndex = index;
        return true;
    }

    public void EndWaypointDrag()
    {
        _draggedWaypointIndex = null;
    }

    /// <summary>
    /// Adds a waypoint to the current route.
    /// </summary>
    /// <param name="latitude">Waypoint latitude in degrees.</param>
    /// <param name="longitude">Waypoint longitude in degrees.</param>
    /// <param name="speed">Waypoint speed.</param>
    /// <returns>The newly created waypoint.</returns>
    public Waypoint AddWaypoint(double latitude, double longitude)
    {
        if (CurrentRoute == null)
            throw new InvalidOperationException(
                "Cannot add a waypoint when no route exists.");

        var waypoint = new Waypoint
        {
            Latitude = latitude,
            Longitude = longitude,
            Speed = DefaultWaypointSpeed
        };

        CurrentRoute.Waypoints.Add(waypoint);
        CurrentRoute.Modified = DateTime.UtcNow;

        return waypoint;
    }

    public void MoveDraggedWaypoint(double latitude, double longitude)
    {
        if (CurrentRoute == null ||
            _draggedWaypointIndex == null)
            return;

        var waypoint =
            CurrentRoute.Waypoints[_draggedWaypointIndex.Value];

        waypoint.Latitude = latitude;
        waypoint.Longitude = longitude;

        CurrentRoute.Modified = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes the current temporary route.
    /// </summary>
    /// <remarks>
    /// This is not currently used by the Tactical Map. It is provided
    /// for the eventual route editing/lifecycle functionality.
    /// </remarks>
    public void ClearRoute()
    {
        CurrentRoute = null;
        IsCreationMode = false;
    }

    public bool SelectWaypoint(int index)
    {
        if (CurrentRoute == null)
            return false;

        if (index < 0 || index >= CurrentRoute.Waypoints.Count)
            return false;

        _selectedWaypointIndex = index;

        Logger.Debug(
            "Selected waypoint {WaypointIndex} for editing.",
            index);

        return true;
    }

    public void AdjustSelectedWaypointSpeed(int delta)
    {
        if (CurrentRoute == null || _selectedWaypointIndex == null)
            return;

        int index = _selectedWaypointIndex.Value;
        if (index < 0 || index >= CurrentRoute.Waypoints.Count)
            return;

        Waypoint waypoint = CurrentRoute.Waypoints[index];

        int newSpeed = (int)(waypoint.Speed + delta);

        waypoint.Speed = Math.Clamp(newSpeed, MinimumWaypointSpeed, MaximumWaypointSpeed);

        CurrentRoute.Modified = DateTime.UtcNow;

        Logger.Debug(
            "Adjusted speed of waypoint {WaypointIndex} by {Delta}. New speed: {NewSpeed}",
            index, delta, waypoint.Speed);
    }

    public bool BeginSpeedEdit(int index)
    {
        if (CurrentRoute == null)
            return false;

        if (index < 0 || index >= CurrentRoute.Waypoints.Count)
            return false;

        _selectedWaypointIndex = index;
        _speedEditOriginalSpeed = (int)(CurrentRoute.Waypoints[index].Speed);

        Logger.Debug(
            "Began speed edit for waypoint {WaypointIndex}. Original speed: {OriginalSpeed}",
            index,
            _speedEditOriginalSpeed.Value);

        return true;
    }

    public void AcceptSpeedEdit()
    {
        if (CurrentRoute == null ||
            _selectedWaypointIndex == null)
            return;

        CurrentRoute.Modified = DateTime.UtcNow;

        _speedEditOriginalSpeed = null;

        Logger.Debug(
            "Accepted speed edit for waypoint {WaypointIndex}. New speed: {NewSpeed}",
            _selectedWaypointIndex.Value,
            CurrentRoute.Waypoints[_selectedWaypointIndex.Value].Speed);
    }

    public void CancelSpeedEdit()
    {
        if (CurrentRoute == null ||
            _selectedWaypointIndex == null ||
            _speedEditOriginalSpeed == null)
            return;

        CurrentRoute.Waypoints[_selectedWaypointIndex.Value].Speed =
            _speedEditOriginalSpeed.Value;

        CurrentRoute.Modified = DateTime.UtcNow;

        _speedEditOriginalSpeed = null;

        Logger.Debug(
            "Cancelled speed edit for waypoint {WaypointIndex}. Reverted to original speed: {OriginalSpeed}",
            _selectedWaypointIndex.Value,
            CurrentRoute.Waypoints[_selectedWaypointIndex.Value].Speed);
    }
}
