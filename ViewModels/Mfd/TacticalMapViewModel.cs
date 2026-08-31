using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GBMS.Services;

namespace GBMS.ViewModels.Mfd;

public class TacticalMapViewModel : ObservableObject, IMfdInputReceiver
{
    public event Action<double, double>? CentreMapOnVehicleRequested;

    public event Action<double>? ZoomLevelChanged;

    public double VehicleLatitude { get; } = 51.2069;
    public double VehicleLongitude { get; } = -1.9770;


    // Zoom levels represent the approximate ground range from
    // the centre of the map, in kilometres.
    private static readonly double[] ZoomLevelsKm =
    {
        5.0, 10.0, 15.0, 20.0, 30.0, 50.0, 75.0, 100.0, 200.0, 500.0
    };

    // Start at the 10 km zoom level.
    private int _zoomLevelIndex = 1;

    public void HandleFunctionKey(MfdFunctionKey key)
    {
        Logger.Debug("SA Tactical Map received function key: {FunctionKey}", key);

        switch (key)
        {
            case MfdFunctionKey.F1:
                ApplyCentreMapOnVehicle();
                break;

            case MfdFunctionKey.F2:
                SelectPreviousZoomLevel();
                break;

            case MfdFunctionKey.F3:
                SelectNextZoomLevel();
                break;

            default:
                Logger.Debug(
                    "SA Tactical Map has no action assigned to {FunctionKey}",
                    key);
                break;
        }
    }

    private void ApplyCentreMapOnVehicle()
    {
        Logger.Information($"Centre map on vehicle requested. Pos: {VehicleLatitude}, {VehicleLongitude}");

        CentreMapOnVehicleRequested?.Invoke(VehicleLatitude, VehicleLongitude);
    }

    private void SelectPreviousZoomLevel()
    {
        if (_zoomLevelIndex > 0)
        {
            _zoomLevelIndex--;

            double zoomLevel = ZoomLevelsKm[_zoomLevelIndex];

            Logger.Information(
                "SA Tactical Map zoom level selected: {ZoomLevel} km", zoomLevel);

            ZoomLevelChanged?.Invoke(zoomLevel);
        }
        else
        {
            Logger.Debug(
                "SA Tactical Map already at minimum zoom level.");
        }
    }

    private void SelectNextZoomLevel()
    {
        if (_zoomLevelIndex < ZoomLevelsKm.Length - 1)
        {
            _zoomLevelIndex++;

            double zoomLevel = ZoomLevelsKm[_zoomLevelIndex];

            Logger.Information(
                "SA Tactical Map zoom level selected: {ZoomLevel} km", zoomLevel);

            ZoomLevelChanged?.Invoke(zoomLevel);
        }
        else
        {
            Logger.Debug(
                "SA Tactical Map already at maximum zoom level.");
        }
    }
}
