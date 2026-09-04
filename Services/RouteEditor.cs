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

    /// <summary>
    /// Gets the index of the waypoint currently being dragged, if any.
    /// </summary>
    public int? DraggedWaypointIndex => _draggedWaypointIndex;

    private int? _selectedWaypointIndex;

    public int? SelectedWaypointIndex => _selectedWaypointIndex;


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
                Id = 0,
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

    /// <summary>
    /// Begins dragging the waypoint at the specified index.
    /// </summary>
    /// <param name="index">The index of the waypoint to drag.</param>
    /// <returns>True if the waypoint drag operation started successfully; otherwise, false.</returns>
    public bool BeginWaypointDrag(int index)
    {
        if (CurrentRoute == null)
            return false;

        if (index < 0 || index >= CurrentRoute.Waypoints.Count)
            return false;

        _draggedWaypointIndex = index;
        return true;
    }

    /// <summary>
    /// Ends the dragging of the currently dragged waypoint.
    /// </summary>
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

    /// <summary>
    /// Moves the currently dragged waypoint to a new location.
    /// </summary>
    /// <param name="latitude">The new latitude of the waypoint in degrees.</param>
    /// <param name="longitude">The new longitude of the waypoint in degrees.</param>
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
    /// Clears the currently selected waypoint.
    /// </summary>
    public void ClearSelection()
    {
        if (SelectedWaypointIndex == null)
            return;

        Logger.Debug(
            "Cleared waypoint selection. Previous index: {WaypointIndex}",
            SelectedWaypointIndex);

        _selectedWaypointIndex = null;
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
}
