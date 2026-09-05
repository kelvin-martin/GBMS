using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
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

    private readonly ObservableCollection<RouteDisplayItem> _displayRoutes = new();

    private int _selectedIndex = -1;
    private int? _lastSelectedRouteId;
    private bool _isDisplayed;

    public bool IsDisplayed => _isDisplayed;

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

        if (_selectedIndex > 0)
        {
            _selectedIndex--;

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

        if (_selectedIndex < _displayRoutes.Count - 1)
        {
            _selectedIndex++;

            UpdateSelectionIndicators();
        }
    }

    /// <summary>
    /// Selects the currently highlighted route for display.
    /// </summary>
    public void Select()
    {
        if (_routeManager == null) return;

        RouteDisplayItem? item = GetSelectedItem();

        if (item == null)
        {
            return;
        }

        _lastSelectedRouteId = item.Route.Id;

        _routeManager.SelectRoute(item.Route.Id);
    }

    /// <summary>
    /// Deletes the currently highlighted route.
    /// </summary>
    public void Delete()
    {
        if (_routeManager == null) return;

        RouteDisplayItem? item = GetSelectedItem();

        if (item == null)
        {
            return;
        }

        _routeManager.RemoveRoute(item.Route.Id);
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
    public sealed class RouteDisplayItem
    {
        public Route Route { get; }

        public string SelectionIndicator { get; set; } = " ";

        public string RouteText => $"R{Route.Id:000}";

        public string ModifiedText =>
            Route.Modified == DateTime.MinValue
                ? string.Empty
                : Route.Modified.ToLocalTime()
                    .ToString(
                        "dd-MMM-yy HH:mm",
                        CultureInfo.InvariantCulture)
                    .ToUpperInvariant();

        public string WaypointText =>
            $"{Route.Waypoints.Count} WP";

        public string FirstWaypointText => FormatFirstWaypoint();

        public RouteDisplayItem(Route route)
        {
            Route = route;
        }

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
