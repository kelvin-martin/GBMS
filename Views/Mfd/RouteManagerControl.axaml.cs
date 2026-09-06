using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using GBMS.Models;
using GBMS.Services;

namespace GBMS.Views.Mfd;

/// <summary>
/// Interaction logic for RouteManagerControl.xaml
/// </summary>
public partial class RouteManagerControl : UserControl, INotifyPropertyChanged
{
    private readonly RouteManager? _routeManager;
    private readonly Messenger? _messenger;

    private readonly ObservableCollection<RouteDisplayItem> _displayRoutes = new();

    private int _selectedIndex = -1;
    private int? _lastSelectedRouteId;
    private bool _isDisplayed;

    // Tracks the accept/delete press sequence for the currently highlighted
    // route. Deliberately independent of RouteManager.CurrentRoute - a route
    // that already happened to be CurrentRoute from an earlier session must
    // not be mistaken for "already pressed once in this sequence", or the
    // very first L4 press on it would jump straight to arming for deletion.
    private int? _lastAcceptedRouteId;
    private bool _pendingDelete;

    public bool IsDisplayed => _isDisplayed;

    /// <summary>
    /// Gets whether a route deletion is currently pending confirmation.
    /// </summary>
    public bool IsPendingDelete => _pendingDelete;

    /// <summary>
    /// Gets the list of routes to display.
    /// </summary>
    public IReadOnlyList<RouteDisplayItem> DisplayRoutes => _displayRoutes;

    public RouteManagerControl()
    {
        InitializeComponent();

        DataContext = this;

        if (!Design.IsDesignMode)
        {
            _routeManager = ApplicationFactory.RouteManager;
            _messenger = ApplicationFactory.Messenger;

            _routeManager.RoutesChanged += OnRoutesChanged;

            RefreshRoutes();
         }
    }

    /// <summary>
    /// Shows or hides the Route Manager control.
    /// </summary>
    public void Show(bool isDisplayed)
    {
        _isDisplayed = isDisplayed;

        IsVisible = isDisplayed;

        if (isDisplayed)
        {
            RestoreSelection();
        }
        else
        {
            CancelPendingDelete();
        }
    }

    /// <summary>
    /// Moves the highlighted route up one position.
    /// </summary>
    public void MoveSelectionUp()
    {
        if (!_isDisplayed || _displayRoutes.Count == 0)
        {
            return;
        }

        CancelPendingDelete();

        if (_selectedIndex > 0)
        {
            _selectedIndex--;
            _lastSelectedRouteId = _displayRoutes[_selectedIndex].Route.Id;
            UpdateSelectionIndicators();
        }
    }

    /// <summary>
    /// Moves the highlighted route down one position.
    /// </summary>
    public void MoveSelectionDown()
    {
        if (!_isDisplayed || _displayRoutes.Count == 0)
        {
            return;
        }

        CancelPendingDelete();

        if (_selectedIndex < _displayRoutes.Count - 1)
        {
            _selectedIndex++;
            _lastSelectedRouteId = _displayRoutes[_selectedIndex].Route.Id;
            UpdateSelectionIndicators();
        }
    }

    /// <summary>
    /// Handles an L4 ("Accept") press while Route Manager is displayed.
    /// Implements the three-press select/arm/confirm sequence:
    /// <list type="bullet">
    /// <item>Press 1 - selects the highlighted route for display (existing
    /// behaviour, unchanged).</item>
    /// <item>Press 2, same route still highlighted - arms it for deletion,
    /// unless it is the route currently assigned to Own Vehicle (which can
    /// never be deleted - this simply falls back to a harmless re-select
    /// instead of arming).</item>
    /// <item>Press 3, same route still armed and highlighted - confirms and
    /// deletes it.</item>
    /// </list>
    /// Cursor movement or hiding Route Manager resets this sequence (see
    /// <see cref="MoveSelectionUp"/>, <see cref="MoveSelectionDown"/>,
    /// <see cref="Show"/>, and <see cref="CancelPendingDelete"/>).
    /// </summary>
    public void RequestAcceptOrDelete()
    {
        if (_routeManager == null) return;

        RouteDisplayItem? item = GetSelectedItem();

        if (item == null)
        {
            return;
        }

        int routeId = item.Route.Id;

        bool isAssigned = ReferenceEquals(
            item.Route, _routeManager.AssignedRoute);

        if (_pendingDelete && _lastAcceptedRouteId == routeId)
        {
            ConfirmDelete(item);
            return;
        }

        if (_lastAcceptedRouteId == routeId && !isAssigned)
        {
            ArmForDeletion(routeId);
            return;
        }

        _lastAcceptedRouteId = routeId;
        _pendingDelete = false;

        _lastSelectedRouteId = routeId;   // restores cursor position on next Show(true)

        _routeManager.SelectRoute(routeId);

        Logger.Debug($"Route {routeId} selected for display.");
    }

    /// <summary>
    /// Arms the given route for deletion (press 2 of the sequence) and
    /// raises the corresponding warning via the application Messenger.
    /// </summary>
    private void ArmForDeletion(int routeId)
    {
        _pendingDelete = true;

        _messenger?.Send(
            $"ROUTE {routeId:000} WILL BE DELETED - PRESS L4 TO CONFIRM",
            isAlert: true);

        Logger.Debug($"Route {routeId} armed for deletion.");
    }

    /// <summary>
    /// Confirms and performs deletion of the given route (press 3 of the
    /// sequence).
    /// </summary>
    private void ConfirmDelete(RouteDisplayItem item)
    {
        if (_routeManager == null) return;

        int routeId = item.Route.Id;

        // This route reached press 3 via press 1 of the same sequence,
        // which already made it CurrentRoute - RemoveRoute refuses to
        // delete CurrentRoute, so it must be cleared first. Checked rather
        // than assumed, consistent with defensive checks used elsewhere
        // (e.g. OwnVehicle's target-waypoint Contains check).
        if (ReferenceEquals(_routeManager.CurrentRoute, item.Route))
        {
            _routeManager.ClearCurrentRoute();
        }

        _routeManager.RemoveRoute(routeId);

        Logger.Debug($"Route {routeId} deleted.");

        CancelPendingDelete();
    }

    /// <summary>
    /// Cancels any pending route deletion and clears the alert bar if a
    /// warning was showing. Safe to call unconditionally - a no-op if
    /// nothing is currently pending.
    /// </summary>
    public void CancelPendingDelete()
    {
        if (_pendingDelete)
        {
            _messenger?.Send("NO ACTIVE WARNINGS", isAlert: false);

            Logger.Debug("Pending route deletion cancelled.");
        }

        _lastAcceptedRouteId = null;
        _pendingDelete = false;
    }

    /// <summary>
    /// Gets the currently selected route display item.
    /// </summary>
    /// <returns>The selected <see cref="RouteDisplayItem"/>, or <c>null</c> if no item is selected.</returns>
    private RouteDisplayItem? GetSelectedItem()
    {
        if (_selectedIndex < 0 ||
            _selectedIndex >= _displayRoutes.Count)
        {
            return null;
        }

        return _displayRoutes[_selectedIndex];
    }

    /// <summary>
    /// Restores the selection to the last selected route or the first route if no previous selection exists.
    /// </summary>
    private void RestoreSelection()
    {
        if (_displayRoutes.Count == 0)
        {
            _selectedIndex = -1;
            return;
        }

        if (_lastSelectedRouteId.HasValue)
        {
            int index = _displayRoutes
                .Select((item, index) => new
                {
                    item.Route.Id,
                    Index = index
                })
                .FirstOrDefault(
                    x => x.Id == _lastSelectedRouteId.Value)
                ?.Index ?? -1;

            if (index >= 0)
            {
                _selectedIndex = index;

                UpdateSelectionIndicators();

                return;
            }
        }

        _selectedIndex = 0;

        UpdateSelectionIndicators();
    }

    /// <summary>
    /// Handles the event when the routes have changed.
    /// </summary>
    private void OnRoutesChanged()
    {
        RefreshRoutes();
    }

    /// <summary>
    /// Refreshes the list of routes to display.
    /// </summary>
    private void RefreshRoutes()
    {
        if (_routeManager == null) return;

        // The route list just changed shape - any pending deletion sequence
        // no longer refers to trustworthy state, so reset it defensively.
        // This also covers the case where ConfirmDelete's own RemoveRoute
        // call triggers this same refresh via RoutesChanged -
        // CancelPendingDelete is idempotent, so no duplicate alert-clear is
        // sent in that case.
        CancelPendingDelete();

        _displayRoutes.Clear();

        IEnumerable<Route> routes = _routeManager.Routes
            .OrderByDescending(r => r.Modified)
            .ThenByDescending(r => r.Id);

        foreach (Route route in routes)
        {
            _displayRoutes.Add(
                new RouteDisplayItem(route));
        }

        NoRoutesText.IsVisible = _displayRoutes.Count == 0;

        RestoreSelection();
    }

    /// <summary>
    /// Updates the selection indicators for all route display items.
    /// </summary>
    private void UpdateSelectionIndicators()
    {
        for (int i = 0; i < _displayRoutes.Count; i++)
        {
            _displayRoutes[i].SelectionIndicator =
                i == _selectedIndex ? ">" : " ";
        }
    }

    /// <summary>
    /// Holds the display information for a route in the Route Manager control.
    /// </summary>
    public sealed class RouteDisplayItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public Route Route { get; }

        private string _selectionIndicator = " ";

        /// <summary>
        /// Gets or sets the selection indicator for the route display item.
        /// </summary>
        public string SelectionIndicator
        {
            get => _selectionIndicator;
            set
            {
                if (_selectionIndicator == value)
                    return;

                _selectionIndicator = value;

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(SelectionIndicator)));
            }
        }

        /// <summary>
        /// Gets the text id  representation of the route.
        /// </summary>
        public string RouteText => $"R{Route.Id:000}";

        /// <summary>
        /// Gets the text representation of the route's last modified date and time.
        /// </summary>
        public string ModifiedText =>
            Route.Modified == DateTime.MinValue
                ? string.Empty
                : Route.Modified.ToLocalTime()
                    .ToString(
                        "dd-MMM-yy HH:mm",
                        CultureInfo.InvariantCulture)
                    .ToUpperInvariant();

        /// <summary>
        /// Gets the text representation of the number of waypoints in the route.
        /// </summary>
        public string WaypointText =>
            $"{Route.Waypoints.Count} WP";

        public string FirstWaypointText => FormatFirstWaypoint();

        public RouteDisplayItem(Route route)
        {
            Route = route;
        }

        /// <summary>
        /// Formats the first waypoint of the route as a string with latitude and longitude.
        /// </summary>
        /// <returns>A string representation of the first waypoint's coordinates.</returns>
        private string FormatFirstWaypoint()
        {
            if (Route.Waypoints.Count == 0)
            {
                return string.Empty;
            }

            Waypoint waypoint = Route.Waypoints[0];

            return $"{FormatLatitude(waypoint.Latitude)} " +
                   $"{FormatLongitude(waypoint.Longitude)}";
        }

        /// <summary>
        /// Formats a latitude value as a string with degrees, minutes, and hemisphere.
        /// </summary>
        /// <param name="latitude">The latitude value to format.</param>
        /// <returns>A string representation of the latitude.</returns>
        private static string FormatLatitude(double latitude)
        {
            char hemisphere = latitude >= 0 ? 'N' : 'S';
            double value = Math.Abs(latitude);

            int degrees = (int)value;
            int minutes = (int)Math.Round((value - degrees) * 60);

            if (minutes == 60)
            {
                degrees++;
                minutes = 0;
            }

            return $"{degrees:00}°{minutes:00}'{hemisphere}";
        }

        /// <summary>
        /// Formats a longitude value as a string with degrees, minutes, and hemisphere.
        /// </summary>
        /// <param name="longitude">The longitude value to format.</param>
        /// <returns>A string representation of the longitude.</returns>
        private static string FormatLongitude(double longitude)
        {
            char hemisphere = longitude >= 0 ? 'E' : 'W';
            double value = Math.Abs(longitude);

            int degrees = (int)value;
            int minutes = (int)Math.Round((value - degrees) * 60);

            if (minutes == 60)
            {
                degrees++;
                minutes = 0;
            }

            return $"{degrees:000}°{minutes:00}'{hemisphere}";
        }
    }
}
