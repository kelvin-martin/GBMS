
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BruTile.FileSystem;
using BruTile.Predefined;
using GBMS.Models;
using GBMS.Services;
using GBMS.ViewModels.Mfd;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using NetTopologySuite.Geometries;
using static GBMS.ViewModels.Mfd.TacticalMapViewModel;

namespace GBMS.Views.Mfd;

/// <summary>
/// Interaction logic for TacticalMapView.xaml
/// </summary>
public partial class TacticalMapView : UserControl
{
    private readonly Bitmap _routeCreationIcon;
    private readonly Bitmap _routeCreationActiveIcon;

    private readonly TacticalSymbolLayer _tacticalSymbolLayer = new TacticalSymbolLayer();
    private readonly MemoryLayer _selectedWaypointLayer = new MemoryLayer();
    private readonly RouteLayer _routeLayer = new RouteLayer();

    private TacticalMapViewModel? _viewModel;

    private readonly RouteEditor _routeEditor;

    private readonly RouteManager? _routeManager;

    private bool _waypointInteraction;

    /// <summary>
    /// Initializes a new instance of the <see cref="TacticalMapView"/> class.
    /// </summary>
    public TacticalMapView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        // InitialiseLocalCache();
        InitialiseOnlineMap();

        MapControl.Map.Layers.Add(_routeLayer.Layer);
        MapControl.Map.Layers.Add(_selectedWaypointLayer);
        MapControl.Map.Layers.Add(_tacticalSymbolLayer.Layer);

        MapControl.MapTapped += OnMapTapped;

        MapControl.PointerPressed += OnMapPointerPressed;
        MapControl.PointerMoved += OnMapPointerMoved;
        MapControl.PointerReleased += OnMapPointerReleased;

        _routeEditor = new RouteEditor();
        _routeEditor.CreationModeChanged += OnRouteCreationModeChanged;

        if (!Design.IsDesignMode)
        {
            _routeManager = ApplicationFactory.RouteManager;
            if (_routeManager == null)
            {
                Logger.Error("RouteManager service is not available.");
            }
            else
            {
                _routeManager.CurrentRouteChanged += OnCurrentRouteChanged;
                _routeManager.AssignedRouteChanged += OnAssignedRouteChanged;
            }
        }

        _routeCreationIcon = new Bitmap(AssetLoader.Open(
        new Uri("avares://GBMS/Assets/Icons/route_creation.png")));

        _routeCreationActiveIcon = new Bitmap(AssetLoader.Open(
                new Uri("avares://GBMS/Assets/Icons/route_creation_active.png")));

        AddMouseCoordinatesWidget();
        AddScaleBarWidget();
    }

    /// <summary>
    /// Adds a mouse coordinates widget to the map.
    /// </summary>
    private void AddMouseCoordinatesWidget()
    {
        var coordinatesWidget = new LatLonMouseCoordinatesWidget()
        {
            Margin = new MRect(10),
            BackColor = new Color(255, 255, 255, 220),
            TextColor = Color.Black,
            Padding = new MRect(8, 4, 8, 4),
            CornerRadius = 4
        };
        MapControl.Map.Widgets.Add(coordinatesWidget);
    }

    /// <summary>
    /// Adds a scale bar widget to the map.
    /// </summary>
    private void AddScaleBarWidget()
    {
        var scaleBar = new ScaleBarWidget(MapControl.Map)
        {
            MaxWidth = 180,

            HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left,
            VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom,

            Margin = new MRect(10),

            TextColor = new Color(0, 70, 180),
            Halo = new Color(255, 255, 255),

            StrokeWidth = 3,
            StrokeWidthHalo = 5,
            TickLength = 8,

            ScaleBarMode = ScaleBarMode.Single,
            TextAlignment = Alignment.Center,

            InputTransparent = true
        }; 

        MapControl.Map.Widgets.Add(scaleBar);
    }

    /// <summary>
    /// Handles the DataContextChanged event.
    /// This method is called whenever the DataContext of the view changes. 
    /// It unsubscribes from events of the previous ViewModel (if any) and subscribes 
    /// to events of the new ViewModel. It also logs the initialization of the 
    /// view with the new ViewModel.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CentreMapOnVehicleRequested -= OnCentreMapOnVehicleRequested;
            _viewModel.ZoomLevelChanged -= OnZoomLevelChanged;
            _viewModel.PanRequested -= OnPanRequested;
            _viewModel.MapUpdateRequested -= OnMapUpdateRequested;
            _viewModel.RouteCreationRequested -= OnRouteCreationRequested;
            _viewModel.FunctionKeyRequested -= HandleFunctionKey;
        }

        _viewModel = DataContext as TacticalMapViewModel;

        if (_viewModel != null)
        {
            Logger.Information(
                "TacticalMapView initialized with ViewModel: {ViewModelType}",
                _viewModel.GetType().Name);

            _viewModel.CentreMapOnVehicleRequested += OnCentreMapOnVehicleRequested;
            _viewModel.ZoomLevelChanged += OnZoomLevelChanged;
            _viewModel.PanRequested += OnPanRequested;
            _viewModel.MapUpdateRequested += OnMapUpdateRequested;
            _viewModel.RouteCreationRequested += OnRouteCreationRequested;
            _viewModel.FunctionKeyRequested += HandleFunctionKey;

            UpdateOwnVehicleSymbol();
        }
    }

    /// <summary>
    /// Initializes an online map layer (e.g., OpenStreetMap).
    /// </summary>
    private void InitialiseOnlineMap()
    {
        // Example of initializing an online map layer (e.g., OpenStreetMap)
        MapControl.Map?.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
    }

    /// <summary>
    /// Initializes the local tile cache for offline map usage.
    /// </summary>
    private void InitialiseLocalCache()
    {
        string localTileFolder = @"C:\Datasets\OpenstreetMap\Falklands";

        // Specify the path to your root map folder
        // This folder should contain the zoom level directories (e.g., /0/, /1/, /2/)
        if (!System.IO.Directory.Exists(localTileFolder))
        {
            Logger.Error("Local map folder does not exist: {localTileFolder}", localTileFolder);
            return;
        }

        // Define the schema (OSM defaults to the Global Spherical Mercator structure)
        // Pass your minimum and maximum available zoom levels (e.g., zoom 0 to 18)
        var tileSchema = new GlobalSphericalMercator(format: "png", minZoomLevel: 0, maxZoomLevel: 11);

        // Create the local file tile source
        // BruTile will automatically look for files matching: folder/{z}/{x}/{y}.png
        var fileTileSource = new FileTileSource(
            tileSchema,
            localTileFolder,
            "png",
            new TimeSpan(long.MaxValue) // Prevents the tile cache from ever expiring
        );

        // Wrap the source into a Mapsui TileLayer
        var offlineLayer = new TileLayer(fileTileSource)
        {
            Name = "OSM PNG Map"
        };

        // Add it to your map control
        MapControl.Map.Layers.Add(offlineLayer);
    }
   
    /// <summary>
    /// Updates the tactical symbol representing the own vehicle on the map.    
    /// </summary>
    private void UpdateOwnVehicleSymbol()
    {
        if (_viewModel == null) return;

        if (_viewModel.OwnVehicleState == null)
        {
            Logger.Warning("OwnVehicle is null in ViewModel. Cannot update tactical symbol.");
            return;
        }

        _tacticalSymbolLayer.SetOwnVehicle(_viewModel.OwnVehicleState);
    }

    /// <summary>
    /// Handles the CentreMapOnVehicleRequested event.
    /// This method is called whenever the ViewModel requests to centre the map on the vehicle's position.
    /// </summary>
    /// <param name="latitude">The latitude of the vehicle.</param>
    /// <param name="longitude">The longitude of the vehicle.</param>
    private void OnCentreMapOnVehicleRequested(double latitude, double longitude)
    {
        const double extentKm = 10.0;

        Logger.Debug("SA Tactical Map centring on vehicle: Lat={Latitude}, Lon={Longitude}",
            latitude, longitude);

        SetMapExtent(latitude, longitude, extentKm);
    }

    /// <summary>
    /// Handles the ZoomLevelChanged event from the ViewModel.
    /// This method is called to update the map's zoom level.
    /// </summary>
    /// <param name="zoomLevelKm">The new zoom level in kilometers.</param>
    private void OnZoomLevelChanged(double zoomLevelKm)
    {
        Logger.Debug("SA Tactical Map applying zoom level: {ZoomLevel} km",
            zoomLevelKm);

        if (_viewModel == null)
        {
            Logger.Warning("Unable to apply zoom level: ViewModel is null.");
            return;
        }

        // For now, use the vehicle position as the centre.
        double latitude = _viewModel.OwnVehicleState?.Position.Latitude ?? 0.0;
        double longitude = _viewModel.OwnVehicleState?.Position.Longitude ?? 0.0;

        SetMapExtent(latitude, longitude, zoomLevelKm);
    }

    /// <summary>
    /// Handles the PanRequested event from the ViewModel.
    /// This method is called to pan the map in the specified direction by the specified distance.
    /// </summary>
    /// <param name="direction">The direction to pan the map.</param>
    /// <param name="distanceKm">The distance to pan the map in kilometers.</param>
    private void OnPanRequested(MapPanDirection direction, double distanceKm)
    {
        Logger.Debug("SA Tactical Map applying pan: Direction={Direction}, Distance={Distance} km",
            direction, distanceKm);

        var navigator = MapControl.Map.Navigator;

        if (navigator == null)
        {
            Logger.Warning("Unable to pan map: navigator unavailable.");
            return;
        }

        var viewport = navigator.Viewport;

        double centerX = viewport.CenterX;
        double centerY = viewport.CenterY;

        // Convert current map centre from Spherical Mercator
        // back to longitude/latitude.
        var currentPosition =
            SphericalMercator.ToLonLat(centerX, centerY);

        double latitude = currentPosition.lat;
        double longitude = currentPosition.lon;

        // Convert the requested ground distance into
        // latitude/longitude offsets.
        double latitudeOffset = distanceKm / Mapping.KMInOneDegreeLat1;

        double longitudeOffset =
            distanceKm / (Mapping.KMInOneDegreeLat1 * Math.Cos(latitude * Math.PI / 180.0));

        switch (direction)
        {
            case MapPanDirection.Left:
                longitude -= longitudeOffset;
                break;

            case MapPanDirection.Right:
                longitude += longitudeOffset;
                break;

            case MapPanDirection.Up:
                latitude += latitudeOffset;
                break;

            case MapPanDirection.Down:
                latitude -= latitudeOffset;
                break;
        }

        // Convert the new centre back to Spherical Mercator.
        var newCenter = SphericalMercator.FromLonLat(longitude, latitude);

        navigator.CenterOn(new MPoint(newCenter.x, newCenter.y));
    }

    /// <summary>
    /// Sets the map extent based on the specified latitude, longitude, and extent in kilometers.
    /// This method calculates the bounding box around the specified centre point and adjusts the map view accordingly.
    /// </summary>
    /// <param name="latitude">The latitude of the centre point.</param>
    /// <param name="longitude">The longitude of the centre point.</param>
    /// <param name="extentKm">The extent of the map in kilometers.</param>
    private void SetMapExtent(double latitude, double longitude, double extentKm)
    {
        var box = Mapping.CalculateMapExtent(latitude, longitude, extentKm);

        MapControl.Map.Navigator?.ZoomToBox(
            box, MBoxFit.Fit);
    }

    /// <summary>
    /// Handles the MapUpdateRequested event from the ViewModel.
    /// This method is called to update the map with the latest simulation state.
    /// </summary>
    private void OnMapUpdateRequested()
    {
        UpdateOwnVehicleSymbol();
        MapControl.RefreshGraphics();
    }

    /// <summary>
    /// Handles the request to start route creation.
    /// This method toggles the route creation mode in the RouteEditor and updates the map accordingly.
    /// </summary>
    private void OnRouteCreationRequested()
    {
        _routeEditor.ToggleCreationMode();

        if (!_routeEditor.IsCreationMode)
        {
            _routeEditor.ClearSelection();

            if ((_routeEditor.CurrentRoute != null) && (_routeManager != null))
            {
                _routeManager.AddRoute(_routeEditor.CurrentRoute);
            }
        }
        else
        {
            RouteManagerControl.Show(false);
        }

        UpdateSelectedWaypointHighlight();
        UpdateSelectedWaypointSpeedDisplay();
        UpdateContextualIcons();
    }

    /// <summary>
    /// Handles the MapTapped event from the MapControl.
    /// This method is called when the user taps on the map, and it adds a new waypoint to 
    /// the current route if route creation mode is enabled.
    /// </summary>  
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        if (!_routeEditor.IsCreationMode) return;

        if (_routeEditor.CurrentRoute == null) return;

        // A pointer interaction with an existing waypoint
        // must not also create a new waypoint.
        if (_waypointInteraction)
        {
            _waypointInteraction = false;
            return;
        }

        // Get the geographic position of the tap.
        MPoint worldPosition = e.WorldPosition;

        var position = SphericalMercator.ToLonLat(
            worldPosition.X, worldPosition.Y);

        Waypoint waypoint = _routeEditor.AddWaypoint(position.lat, position.lon);

        _routeLayer.SetRoute(_routeEditor.CurrentRoute, false);

        MapControl.RefreshGraphics();

        Logger.Information("Added waypoint to route: Lat={Latitude}, Lon={Longitude}, Speed={Speed} km/h",
            waypoint.Latitude, waypoint.Longitude, waypoint.Speed);
    }

    /// <summary>
    /// Finds the index of a waypoint at the specified geographic position.
    /// </summary>
    /// <param name="latitude">The latitude of the position in degrees.</param>
    /// <param name="longitude">The longitude of the position in degrees.</param>
    /// <returns>The index of the waypoint if found; otherwise, null.</returns>
    private int? FindWaypointAtPosition(double latitude, double longitude)
    {
        if (_routeEditor.CurrentRoute == null) return null;

        // Allow a meaningful geographic area around each waypoint.
        const double hitRadiusKm = 0.5;

        for (int i = 0; i < _routeEditor.CurrentRoute.Waypoints.Count; i++)
        {
            var waypoint = _routeEditor.CurrentRoute.Waypoints[i];

            double distanceKm = Mapping.CalculateDistanceKm(
                latitude, longitude,
                waypoint.Latitude, waypoint.Longitude);

            if (distanceKm <= hitRadiusKm)
                return i;
        }

        return null;
    }

    /// <summary>
    /// Handles the PointerPressed event on the map control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnMapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_routeEditor.IsCreationMode ||
            _routeEditor.CurrentRoute == null)
            return;

        var point = e.GetCurrentPoint(MapControl);

        // Right mouse button clears the current waypoint selection.
        if (point.Properties.IsRightButtonPressed)
        {
            if (_routeEditor.SelectedWaypointIndex != null)
            {
                _routeEditor.ClearSelection();

                UpdateSelectedWaypointHighlight();
                UpdateSelectedWaypointSpeedDisplay();
                UpdateContextualIcons();

                Logger.Debug("Waypoint selection cleared by right mouse button.");
            }

            return;
        }

        // Only process left mouse button for waypoint interaction.
        if (!point.Properties.IsLeftButtonPressed)
            return;

        MPoint worldPosition =
            MapControl.Map.Navigator.Viewport.ScreenToWorld(
                point.Position.X, point.Position.Y);

        var lonLat = SphericalMercator.ToLonLat(
            worldPosition.X, worldPosition.Y);

        int? waypointIndex = FindWaypointAtPosition(
            lonLat.lat, lonLat.lon);

        if (waypointIndex == null)
            return;

        if (!_routeEditor.SelectWaypoint(waypointIndex.Value))
            return;

        UpdateSelectedWaypointHighlight();
        UpdateSelectedWaypointSpeedDisplay();
        UpdateContextualIcons();

        if (!_routeEditor.BeginWaypointDrag(waypointIndex.Value))
            return;

        _waypointInteraction = true;

        // Take ownership of the pointer and prevent Mapsui
        // from panning the map while dragging the waypoint.
        e.Pointer.Capture(MapControl);
        MapControl.Map.Navigator.PanLock = true;

        Logger.Debug(
            "Started dragging waypoint {WaypointIndex}.",
            waypointIndex.Value + 1);
    }

    /// <summary>
    /// Handles the PointerMoved event on the map control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnMapPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_routeEditor.DraggedWaypointIndex == null) return;

        if (_routeEditor.CurrentRoute == null) return;

        var point = e.GetCurrentPoint(MapControl);

        MPoint worldPosition =
            MapControl.Map.Navigator.Viewport.ScreenToWorld(
                point.Position.X, point.Position.Y);

        var position = SphericalMercator.ToLonLat(
            worldPosition.X, worldPosition.Y);

        _routeEditor.MoveDraggedWaypoint(
            position.lat, position.lon);

        UpdateSelectedWaypointHighlight();

        _routeLayer.SetRoute(_routeEditor.CurrentRoute, false);

        MapControl.RefreshGraphics();
    }

    /// <summary>
    /// Handles the PointerReleased event on the map control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnMapPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_routeEditor.DraggedWaypointIndex != null)
        {
            Logger.Debug(
                "Finished dragging waypoint {WaypointIndex}.",
                _routeEditor.DraggedWaypointIndex.Value + 1);
        }

        _routeEditor.EndWaypointDrag();

        UpdateSelectedWaypointHighlight();

        // Give normal map panning control back to Mapsui.
        MapControl.Map.Navigator.PanLock = false;

        // Release pointer capture.
        e.Pointer.Capture(null);
    }

    /// <summary>
    /// Handles changes to the route creation mode.
    /// </summary>
    /// <param name="active">Indicates whether route creation mode is active.</param>
    private void OnRouteCreationModeChanged(bool active)
    {
        RouteCreationIcon.Source =
            active ? _routeCreationActiveIcon : _routeCreationIcon;
    }

    /// <summary>
    /// Handles function key presses.
    /// This method determines the current mode (normal map or route creation) 
    /// and delegates the handling of the function key press to the appropriate method.
    /// </summary>
    /// <param name="key">The function key that was pressed.</param>
    private void HandleFunctionKey(MfdFunctionKey key)
    {
        if (!_routeEditor.IsCreationMode)
        {
            HandleNormalMapFunctionKey(key);
            return;
        }

        if (_routeEditor.SelectedWaypointIndex != null)
        {
            HandleSpeedEditFunctionKey(key);
            return;
        }
    }

    /// <summary>
    /// Handles function key presses when in normal map mode.
    /// This method processes function keys related to the Route Manager and updates the map accordingly.
    /// </summary>
    /// <param name="key">The function key that was pressed.</param>
    private void HandleNormalMapFunctionKey(MfdFunctionKey key)
    {
        switch (key)
        {
            case MfdFunctionKey.L3:
                RouteManagerControl.Show(
                    !RouteManagerControl.IsDisplayed);
                UpdateContextualIcons();
                break;
            case MfdFunctionKey.L4:
                if (RouteManagerControl.IsDisplayed)
                {
                    RouteManagerControl.Select();
                }
                else if (_routeManager != null && _routeManager.CurrentRoute != null &&
                    !ReferenceEquals(_routeManager.CurrentRoute, _routeManager.AssignedRoute))
                {
                    _routeManager.AssignRoute(_routeManager.CurrentRoute.Id);
                }
                break;
            case MfdFunctionKey.L5:
                if (RouteManagerControl.IsDisplayed)
                {
                    RouteManagerControl.MoveSelectionUp();
                }
                break;
            case MfdFunctionKey.L6:
                if (RouteManagerControl.IsDisplayed)
                {
                    RouteManagerControl.MoveSelectionDown();
                }
                break;
        }
    }

    /// <summary>
    /// Handles function key presses when in speed edit mode for a selected waypoint.
    /// </summary>
    /// <param name="key"></param>
    private void HandleSpeedEditFunctionKey(MfdFunctionKey key)
    {
        switch (key)
        {
            case MfdFunctionKey.L5:
                if (_routeEditor.SelectedWaypointIndex == null)
                    return;

                _routeEditor.AdjustSelectedWaypointSpeed(+1);

                _routeLayer.SetRoute(_routeEditor.CurrentRoute, false!);
                UpdateSelectedWaypointSpeedDisplay();
                MapControl.RefreshGraphics();
                break;

            case MfdFunctionKey.L6:
                if (_routeEditor.SelectedWaypointIndex == null)
                    return;

                _routeEditor.AdjustSelectedWaypointSpeed(-1);

                _routeLayer.SetRoute(_routeEditor.CurrentRoute!, false);
                UpdateSelectedWaypointSpeedDisplay();
                MapControl.RefreshGraphics();
                break;
        }
    }

    /// <summary>
    /// Updates the visibility of contextual icons based on the current state of the RouteEditor.
    /// </summary>
    private void UpdateContextualIcons()
    {
        // L1 is always available.
        CentreVehicleIcon.IsVisible = true;

        if (!_routeEditor.IsCreationMode)
        {
            // Normal Tactical Map mode.
            SpeedIncreaseIndicator.IsVisible = false;
            SpeedDecreaseIndicator.IsVisible = false;
            RouteManagerIcon.IsVisible = true;

            // When the Route Manager is displayed, L5/L6 navigate
            // through the route collection.
            bool routeManagerDisplayed =
                RouteManagerControl.IsDisplayed;

            CursorUpIndicator.IsVisible = routeManagerDisplayed;
            CursorDownIndicator.IsVisible = routeManagerDisplayed;
            AcceptRouteIndicator.IsVisible = routeManagerDisplayed;
            AssignedRouteIndicator.IsVisible = false;


            if ((_routeManager?.CurrentRoute != null) && (_routeManager.AssignedRoute == null))
            {
                AssignedRouteIndicator.IsVisible = true;
            }

            return;
        }

        RouteManagerIcon.IsVisible = false;

        // Route creation/editing mode.
        bool waypointSelected =
            _routeEditor.SelectedWaypointIndex != null;

        // Speed editing mode.
        SpeedIncreaseIndicator.IsVisible = waypointSelected;
        SpeedDecreaseIndicator.IsVisible = waypointSelected;
    }


    /// <summary>
    /// Updates the highlight for the selected waypoint on the map.
    /// </summary>
    private void UpdateSelectedWaypointHighlight()
    {
        _selectedWaypointLayer.Features = [];

        if (_routeEditor.CurrentRoute == null) return;

        int? selectedIndex = _routeEditor.SelectedWaypointIndex;

        if (selectedIndex == null) return;

        if (selectedIndex.Value < 0 ||
            selectedIndex.Value >= _routeEditor.CurrentRoute.Waypoints.Count)
            return;

        Waypoint waypoint =
            _routeEditor.CurrentRoute.Waypoints[selectedIndex.Value];

        // Create a small geodesic selection halo around
        // the selected waypoint.
        List<MPoint> points = Mapping.CreateGeodesicCircle(
            waypoint.Latitude, waypoint.Longitude,
            0.25);       // 250 metres

        var coordinates = points
            .Select(p => new Coordinate(p.X, p.Y)).ToList();

        // Close the polygon ring.
        if (coordinates.Count > 0 &&
            !coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(coordinates[0]);
        }

        var ring = new LinearRing(coordinates.ToArray());

        var polygon = new Polygon(ring);

        var feature = new GeometryFeature(polygon);

        feature.Styles.Add(
            new VectorStyle
            {
                Fill = null,
                Outline = new Pen(
                    Color.Green,
                    3)
            });

        _selectedWaypointLayer.Features = [feature];

        MapControl.RefreshGraphics();
    }

    /// <summary>
    /// Updates the display of the speed for the selected waypoint.
    /// </summary>
    private void UpdateSelectedWaypointSpeedDisplay()
    {
        int? selectedIndex = _routeEditor.SelectedWaypointIndex;

        if (selectedIndex == null ||
            _routeEditor.CurrentRoute == null ||
            selectedIndex.Value < 0 ||
            selectedIndex.Value >= _routeEditor.CurrentRoute.Waypoints.Count)
        {
            SpeedEditPanel.IsVisible = false;
            return;
        }

        var waypoint =
            _routeEditor.CurrentRoute.Waypoints[selectedIndex.Value];

        SpeedEditPanel.Speed = (int) waypoint.Speed;
        SpeedEditPanel.IsVisible = true;
    }

    /// <summary>
    /// Handles the event when the CurrentRouteChanged event from the RouteManager.
    /// This method updates the route layer on the map to reflect the new current route.
    /// </summary>
    /// <param name="route">The new current route, or <c>null</c> if there is no current route.</param>
    private void OnCurrentRouteChanged(Route? route)
    {
        if (route == null) return;

        _routeEditor.SetRoute(route);

        bool isAssigned = ReferenceEquals(route, _routeManager?.AssignedRoute);

        _routeLayer.SetRoute(route, isAssigned);
        MapControl.RefreshGraphics();
    }

    /// <summary>
    /// Handles the AssignedRouteChanged event from the RouteManager.
    /// Restyles the currently displayed route if its assigned status has changed
    /// - e.g. assigning the route currently on display should restyle it immediately,
    /// without CurrentRoute itself changing.
    /// </summary>
    /// <param name="route">The newly assigned route, or <c>null</c> if none is assigned.</param>
    private void OnAssignedRouteChanged(Route? route)
    {
        Route? displayedRoute = _routeManager?.CurrentRoute;

        if (displayedRoute == null) return;

        bool isAssigned = ReferenceEquals(displayedRoute, route);

        _routeLayer.SetRoute(displayedRoute, isAssigned);
        MapControl.RefreshGraphics();
    }
}