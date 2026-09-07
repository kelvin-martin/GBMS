using System;
using System.Collections.Generic;
using System.Linq;
using GBMS.Models;

namespace GBMS.Services;

public static class ApplicationFactory
{
    private static SimulationManager? _simulationManager;

    private static Messenger? _messenger;

    private static RouteManager? _routeManager;

    private static readonly List<AppMessage> _startupAlerts = new();

    /// <summary>
    /// Initialises the application services.
    /// </summary>
    public static void Initialize()
    {
        if (_simulationManager != null || _messenger != null)
        {
            throw new InvalidOperationException(
                "ApplicationFactory has already been initialized.");
        }

        _messenger = new Messenger();



        // Vehicle configuration - see GBMS_Vehicle_Configuration_Design.md.
        var vehicleTypePersistence = new VehicleTypePersistence();
        var scenarioPersistence = new ScenarioPersistence();

        IReadOnlyList<VehicleType> vehicleTypes = vehicleTypePersistence.LoadAll();

        if (vehicleTypes.Count == 0)
        {
            RecordStartupAlert("No valid vehicle types found - using default vehicle.");
            vehicleTypes = new[] { DefaultConfigurationAssets.LoadVehicleType() };
        }

        ScenarioConfiguration? scenario = scenarioPersistence.Load();
        VehicleType resolvedVehicleType;

        if (scenario == null)
        {
            RecordStartupAlert("No valid scenario configuration found - using default scenario.");
            scenario = DefaultConfigurationAssets.LoadScenario();

            // The default scenario references the default vehicle type by
            // definition - resolve it directly rather than searching the loaded
            // library, which has no reason to contain a matching entry.
            resolvedVehicleType = DefaultConfigurationAssets.LoadVehicleType();
        }
        else
        {
            resolvedVehicleType =
                vehicleTypes.FirstOrDefault(t => t.VehicleTypeId == scenario.VehicleTypeId)
                ?? DefaultConfigurationAssets.LoadVehicleType();

            if (!vehicleTypes.Any(t => t.VehicleTypeId == scenario.VehicleTypeId))
            {
                RecordStartupAlert(
                    $"Unknown vehicle type '{scenario.VehicleTypeId}' - using default vehicle.");
            }
        }

        Logger.Information(
            "Resolved vehicle configuration: {VehicleTypeId} ({Min}-{Max} km/h, {Turn} deg/s), start ({Lat}, {Lon}) heading {Heading}",
            resolvedVehicleType.VehicleTypeId, resolvedVehicleType.MinSpeedKmh, resolvedVehicleType.MaxSpeedKmh,
            resolvedVehicleType.MaxTurnRateDegreesPerSecond, scenario.StartLatitude, scenario.StartLongitude,
            scenario.StartHeading);

        var persistence = new RoutePersistence();
        _routeManager = new RouteManager(persistence);

        _simulationManager = new SimulationManager(resolvedVehicleType, scenario);
    }

    /// <summary>
    /// Gets the application-wide SimulationManager.
    /// </summary>
    public static SimulationManager SimulationManager =>
        _simulationManager ?? throw new InvalidOperationException(
            "ApplicationFactory has not been initialized.");

    /// <summary>
    /// Gets the application-wide Messenger.
    /// </summary>
    public static Messenger Messenger =>
    _messenger ?? throw new InvalidOperationException(
        "ApplicationFactory has not been initialized.");

    /// <summary>
    /// Gets the application-wide RouteManager.
    /// </summary>
    public static RouteManager RouteManager =>
    _routeManager ?? throw new InvalidOperationException(
        "ApplicationFactory has not been initialized.");

    /// <summary>
    /// Messages raised while Initialize() was running, before any UI
    /// existed to display them - e.g. configuration fallbacks. Read once
    /// by the first UI component ready to show them (see MfdViewModel).
    /// Not domain-specific - any startup step can record here, not just
    /// vehicle configuration. See GBMS_Vehicle_Configuration_Design.md §5.
    /// </summary>
    public static IReadOnlyList<AppMessage> StartupAlerts => _startupAlerts;

    /// <summary>
    /// Records a message raised during startup, for later display once
    /// the UI is ready. Also logs it immediately, since Initialize() runs
    /// before any UI-facing alert mechanism exists.
    /// </summary>
    internal static void RecordStartupAlert(string text, bool isAlert = true)
    {
        _startupAlerts.Add(new AppMessage(text, isAlert));

        if (isAlert)
            Logger.Warning(text);
        else
            Logger.Information(text);
    }
}