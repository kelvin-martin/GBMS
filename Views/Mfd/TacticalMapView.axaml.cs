
using System;
using Avalonia.Controls;
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
using Mapsui.UI.Avalonia;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;

using static GBMS.ViewModels.Mfd.TacticalMapViewModel;

namespace GBMS.Views.Mfd;

public partial class TacticalMapView : UserControl
{
    const double KMInOneDegreeLat = 111.0; // approximate conversion factor for latitude degrees to kilometers

    private readonly TacticalSymbolLayer _tacticalSymbolLayer = new TacticalSymbolLayer();

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

        // Add the mouse coordinates widget to the map.
        AddMouseCoordinatesWidget();

        // Add the scale bar widget to the map.
        AddScaleBarWidget();

    }

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


    private void UpdateOwnVehicleSymbol()
    {
        if (_viewModel == null)
            return;

        if (_viewModel.OwnVehicle == null)
        {
            Logger.Warning("OwnVehicle is null in ViewModel. Cannot update tactical symbol.");
            return;
        }

        _tacticalSymbolLayer.SetOwnVehicle(_viewModel.OwnVehicle);
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

    private void OnZoomLevelChanged(double zoomLevelKm)
    {
        Logger.Debug("SA Tactical Map applying zoom level: {ZoomLevel} km",
            zoomLevelKm);

        // For now, use the vehicle position as the centre.
        double latitude = _viewModel?.OwnVehicle?.Position.Latitude ?? 0.0;
        double longitude = _viewModel?.OwnVehicle?.Position.Longitude ?? 0.0;

        SetMapExtent(latitude, longitude, zoomLevelKm);
    }

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
}