using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GBMS.Services;

namespace GBMS.ViewModels.Mfd;

public class TacticalMapViewModel : ObservableObject, IMfdInputReceiver
{
    public event Action<double, double>? CentreMapOnVehicleRequested;

    public event Action<double>? ZoomLevelChanged;

    public event Action<MapPanDirection, double>? PanRequested;

    // placeholder for vehicle position,  this would be dynamic
    public double VehicleLatitude { get; } = 51.2069;
    public double VehicleLongitude { get; } = -1.9770;

    public enum MapPanDirection
    {
        Left, Right, Up, Down
    }

    // Zoom levels represent the approximate ground range from
    // the centre of the map, in kilometres.
    private static readonly double[] ZoomLevelsKm =
    {
        2.0, 5.0, 10.0, 15.0, 20.0, 30.0, 50.0, 75.0, 100.0, 200.0, 500.0
    };

    // Start at the 10 km zoom level.
    private int _zoomLevelIndex = 2;

    public void HandleFunctionKey(MfdFunctionKey key)
    {
        Logger.Debug("SA Tactical Map received function key: {FunctionKey}", key);

        switch (key)
        {
            case MfdFunctionKey.L1:
                ApplyCentreMapOnVehicle();
                break;

            case MfdFunctionKey.R1:
                SelectPreviousZoomLevel();
                break;

            case MfdFunctionKey.R2:
                SelectNextZoomLevel();
                break;

            case MfdFunctionKey.R3:
                SelectPan(MapPanDirection.Left);
                break;

            case MfdFunctionKey.R4:
                SelectPan(MapPanDirection.Right);
                break;

            case MfdFunctionKey.R5:
                    SelectPan(MapPanDirection.Up);
                break;

            case MfdFunctionKey.R6:
                    SelectPan(MapPanDirection.Down);
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

            Logger.Debug(
                "SA Tactical Map zoom level selected: {ZoomLevel} km", ZoomLevelsKm[_zoomLevelIndex]);

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

            Logger.Debug(
                "SA Tactical Map zoom level selected: {ZoomLevel} km", ZoomLevelsKm[_zoomLevelIndex]);

            ZoomLevelChanged?.Invoke(zoomLevel);
        }
        else
        {
            Logger.Debug(
                "SA Tactical Map already at maximum zoom level.");
        }
    }

    private void SelectPan(MapPanDirection direction)
    {
        double panDistanceKm = ZoomLevelsKm[_zoomLevelIndex] * 0.25;

        Logger.Debug(
            "SA Tactical Map pan {Direction} requested: {Distance} km.",
            direction, panDistanceKm);

        PanRequested?.Invoke(direction, panDistanceKm);
    }
}
