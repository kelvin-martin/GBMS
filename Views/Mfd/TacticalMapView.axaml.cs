
using System;
using Avalonia.Controls;
using Avalonia.Input;
using BruTile.FileSystem;
using BruTile.Predefined;
using GBMS.Models;
using GBMS.Services;
using GBMS.ViewModels.Mfd;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI;
using Mapsui.UI.Avalonia;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;

using static GBMS.ViewModels.Mfd.TacticalMapViewModel;

namespace GBMS.Views.Mfd;

public partial class TacticalMapView : UserControl
{
    private Route? _currentRoute;
    private bool _routeCreationMode;

    private int? _draggedWaypointIndex;
    private bool _waypointDragging;

    const double KMInOneDegreeLat = 111.0; // approximate conversion factor for latitude degrees to kilometers

    private readonly TacticalSymbolLayer _tacticalSymbolLayer = new TacticalSymbolLayer();

    private readonly RouteLayer _routeLayer = new RouteLayer();

    private TacticalMapViewModel? _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="TacticalMapView"/> class.
    /// </summary>
    public TacticalMapView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        // InitialiseLocalCache();
        InitialiseOnlineMap();

        MapControl.Map.Layers.Add(_tacticalSymbolLayer.Layer);
        MapControl.Map.Layers.Add(_routeLayer.Layer);

        MapControl.MapTapped += OnMapTapped;

        MapControl.PointerPressed += OnMapPointerPressed;
        MapControl.PointerMoved += OnMapPointerMoved;
        MapControl.PointerReleased += OnMapPointerReleased;

        // Add the mouse coordinates widget to the map.
        AddMouseCoordinatesWidget();

        // Add the scale bar widget to the map.
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

            _viewModel.RouteCreationRequested -= OnRouteCreationRequested;
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
        if (_viewModel == null)
            return;

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
        double latitudeOffset = distanceKm / KMInOneDegreeLat;

        double longitudeOffset =
            distanceKm / (KMInOneDegreeLat * Math.Cos(latitude * Math.PI / 180.0));

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
        double latitudeOffset = extentKm / KMInOneDegreeLat;

        double longitudeOffset =
            extentKm / (KMInOneDegreeLat * Math.Cos(latitude * Math.PI / 180.0));

        double minLatitude = latitude - latitudeOffset;
        double maxLatitude = latitude + latitudeOffset;

        double minLongitude = longitude - longitudeOffset;
        double maxLongitude = longitude + longitudeOffset;

        var min = SphericalMercator.FromLonLat(
            minLongitude, minLatitude);

        var max = SphericalMercator.FromLonLat(
            maxLongitude, maxLatitude);

        var box = new MRect(
            min.x, min.y,
            max.x, max.y);

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
    /// </summary>
    private void OnRouteCreationRequested()
    {
        StartRouteCreation();
    }

    private void StartRouteCreation()
    {
        _currentRoute = new Route
        {
            Id = Guid.NewGuid(),
            Name = "Temporary Route",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };

        _routeCreationMode = true;
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
        if (!_routeCreationMode || _currentRoute == null)
            return;

        // Get the geographic position of the click.
        MPoint worldPosition = e.WorldPosition;

        var position = SphericalMercator.ToLonLat(
            worldPosition.X,
            worldPosition.Y);

        var waypoint = new Waypoint
        {
            Latitude = position.lat,
            Longitude = position.lon,
            Speed = 0
        };

        _currentRoute.Waypoints.Add(waypoint);
        _currentRoute.Modified = DateTime.UtcNow;

        _routeLayer.SetRoute(_currentRoute);

        MapControl.RefreshGraphics();

        Logger.Information(
            "Added waypoint to route: Lat={Latitude}, Lon={Longitude}",
            waypoint.Latitude, waypoint.Longitude);
    }


    private static double CalculateDistanceKm(
    double latitude1,
    double longitude1,
    double latitude2,
    double longitude2)
    {
        const double earthRadiusKm = 6371.0;

        double lat1 = Math.PI * latitude1 / 180.0;
        double lat2 = Math.PI * latitude2 / 180.0;

        double deltaLat = lat2 - lat1;
        double deltaLon =
            Math.PI * (longitude2 - longitude1) / 180.0;

        double a =
            Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
            Math.Cos(lat1) *
            Math.Cos(lat2) *
            Math.Sin(deltaLon / 2) *
            Math.Sin(deltaLon / 2);

        double c =
            2.0 * Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(1.0 - a));

        return earthRadiusKm * c;
    }


    private int? FindWaypointAtPosition(double latitude, double longitude)
    {
        if (_currentRoute == null)
            return null;

        // Allow a meaningful geographic area around each waypoint.
        const double hitRadiusKm = 0.5;

        for (int i = 0; i < _currentRoute.Waypoints.Count; i++)
        {
            var waypoint = _currentRoute.Waypoints[i];

            double distanceKm = CalculateDistanceKm(
                latitude,
                longitude,
                waypoint.Latitude,
                waypoint.Longitude);

            if (distanceKm <= hitRadiusKm)
                return i;
        }

        return null;
    }


    private void OnMapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_routeCreationMode || _currentRoute == null)
            return;

        var point = e.GetCurrentPoint(MapControl);

        if (!point.Properties.IsLeftButtonPressed)
            return;

        MPoint worldPosition =
            MapControl.Map.Navigator.Viewport.ScreenToWorld(
                point.Position.X,
                point.Position.Y);

        var lonLat = SphericalMercator.ToLonLat(
            worldPosition.X,
            worldPosition.Y);

        int? waypointIndex = FindWaypointAtPosition(
            lonLat.lat,
            lonLat.lon);

        if (waypointIndex == null)
            return;

        _draggedWaypointIndex = waypointIndex;
        _waypointDragging = true;

        // Take ownership of the pointer and prevent Mapsui from panning.
        e.Pointer.Capture(MapControl);
        MapControl.Map.Navigator.PanLock = true;

        Logger.Debug(
            "Started dragging waypoint {WaypointIndex}.", waypointIndex.Value + 1);
    }

    private void OnMapPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_waypointDragging ||
            _draggedWaypointIndex == null ||
            _currentRoute == null)
            return;

        int waypointIndex = _draggedWaypointIndex.Value;

        if (waypointIndex < 0 ||
            waypointIndex >= _currentRoute.Waypoints.Count)
            return;

        var point = e.GetCurrentPoint(MapControl);

        MPoint worldPosition =
            MapControl.Map.Navigator.Viewport.ScreenToWorld(
                point.Position.X,
                point.Position.Y);

        var position = SphericalMercator.ToLonLat(
            worldPosition.X,
            worldPosition.Y);

        var waypoint = _currentRoute.Waypoints[waypointIndex];

        waypoint.Latitude = position.lat;
        waypoint.Longitude = position.lon;

        _currentRoute.Modified = DateTime.UtcNow;

        _routeLayer.SetRoute(_currentRoute);

        MapControl.RefreshGraphics();
    }

    private void OnMapPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_waypointDragging)
        {
            Logger.Debug(
                "Finished dragging waypoint {WaypointIndex}.", (_draggedWaypointIndex ?? -1) + 1);
        }

        _draggedWaypointIndex = null;
        _waypointDragging = false;

        // Always give normal map panning control back to Mapsui.
        MapControl.Map.Navigator.PanLock = false;

        // Release pointer capture.
        e.Pointer.Capture(null);
    }

}