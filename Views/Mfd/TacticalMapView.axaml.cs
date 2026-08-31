
using System;
using Avalonia.Controls;
using BruTile.FileSystem;
using BruTile.Predefined;
using GBMS.Services;
using GBMS.ViewModels.Mfd;
using Mapsui;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using Mapsui.Projections;

namespace GBMS.Views.Mfd;

public partial class TacticalMapView : UserControl
{

    private TacticalMapViewModel? _viewModel;

    public TacticalMapView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        // InitialiseLocalCache();
        InitialiseOnlineMap();
    }


    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CentreMapOnVehicleRequested -=
                OnCentreMapOnVehicleRequested;

            _viewModel.ZoomLevelChanged -=
                OnZoomLevelChanged;
        }

        _viewModel = DataContext as TacticalMapViewModel;

        if (_viewModel != null)
        {
            Logger.Information(
                "TacticalMapView initialized with ViewModel: {ViewModelType}",
                _viewModel.GetType().Name);

            _viewModel.CentreMapOnVehicleRequested +=
                OnCentreMapOnVehicleRequested;

            _viewModel.ZoomLevelChanged +=
                OnZoomLevelChanged;
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

    private void OnCentreMapOnVehicleRequested(double latitude, double longitude)
    {
        const double extentKm = 10.0;

        Logger.Information(
            "SA Tactical Map centring on vehicle: Lat={Latitude}, Lon={Longitude}",
            latitude, longitude);

        SetMapExtent(
               latitude, longitude, extentKm);
    }

    private void OnZoomLevelChanged(double zoomLevelKm)
    {
        Logger.Information(
            "SA Tactical Map applying zoom level: {ZoomLevel} km",
            zoomLevelKm);

        // For now, use the vehicle position as the centre.
        double latitude = _viewModel?.VehicleLatitude ?? 0.0;
        double longitude = _viewModel?.VehicleLongitude ?? 0.0;

        SetMapExtent(latitude, longitude, zoomLevelKm);
    }

    private void SetMapExtent(double latitude, double longitude, double extentKm)
    {
        double latitudeOffset = extentKm / 111.0;

        double longitudeOffset =
            extentKm / (111.0 * Math.Cos(latitude * Math.PI / 180.0));

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