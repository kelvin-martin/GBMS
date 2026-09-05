using System;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GBMS.Services;
using GBMS.Simulation;
using Mapsui.UI.Avalonia;

namespace GBMS.ViewModels.Mfd;

/// <summary>
/// ViewModel for the TacticalMapView, handling map interactions and simulation state updates.
/// </summary>
public class TacticalMapViewModel : ObservableObject, IMfdInputReceiver
{
    public OwnVehicleState? OwnVehicleState => _simManager?.SimulationState.OwnVehicle;
    
    public event Action<double, double>? CentreMapOnVehicleRequested;

    public event Action<double>? ZoomLevelChanged;

    public event Action<MapPanDirection, double>? PanRequested;

    public event Action? MapUpdateRequested;

    public event Action? RouteCreationRequested;

    public event Action<MfdFunctionKey>? FunctionKeyRequested;

    private readonly SimulationManager? _simManager;

    private readonly DispatcherTimer? _simulationStateTimer;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="TacticalMapViewModel"/> class.
    /// </summary>
    public TacticalMapViewModel()
    {
        if (!Design.IsDesignMode)
        {
            _simManager = ApplicationFactory.SimulationManager;

            _simulationStateTimer = new DispatcherTimer
            {
                // UI presentation rate.
                Interval = TimeSpan.FromMilliseconds(100)
            };

            _simulationStateTimer.Tick += OnSimulationStateTimerTick;
            _simulationStateTimer.Start();
        }
    }

    /// <summary>
    /// Handles a function key press from the MFD.
    /// </summary>
    /// <param name="key">The function key that was pressed.</param>
    public void HandleFunctionKey(MfdFunctionKey key)
    {
        switch (key)
        {
            case MfdFunctionKey.L1:
                ApplyCentreMapOnVehicle();
                break;

            case MfdFunctionKey.L2:
                RequestRouteCreation();
                break;

            case MfdFunctionKey.L3:
                FunctionKeyRequested?.Invoke(MfdFunctionKey.L3);
                break;

            case MfdFunctionKey.L4:
                FunctionKeyRequested?.Invoke(MfdFunctionKey.L4);
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

    /// <summary>
    /// Handles the action to centre the map on the own vehicle's position.
    /// </summary>
    private void ApplyCentreMapOnVehicle()
    {
        if (_simManager == null)
        {
            Logger.Warning("Centre map on vehicle requested, but SimulationManager is null.");
            return;
        }

        var ownVehicleState = _simManager.SimulationState.OwnVehicle;
        if (ownVehicleState == null)
            return;

        CentreMapOnVehicleRequested?.Invoke(ownVehicleState.Position.Latitude, ownVehicleState.Position.Longitude);
    }

    /// <summary>
    /// Selects the previous zoom level for the map.
    /// </summary>
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

    /// <summary>
    /// Selects the next zoom level for the map.
    /// </summary>
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

    /// <summary>
    /// Handles the tick event of the simulation state timer.
    /// This method is called periodically to update the map with the latest simulation state.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OnSimulationStateTimerTick(object? sender, EventArgs e)
    {
        if (_simManager == null)
            return;

        var ownVehicleState = _simManager.SimulationState.OwnVehicle;
        if (ownVehicleState == null)
            return;

        MapUpdateRequested?.Invoke();
    }

    /// <summary>
    /// Handles the request to start route creation.
    /// </summary>
    private void RequestRouteCreation()
    {
        Logger.Debug("SA Tactical Map route creation requested.");

        RouteCreationRequested?.Invoke();
    }


}
