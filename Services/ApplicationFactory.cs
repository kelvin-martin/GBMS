using System;
using CommunityToolkit.Mvvm.Messaging;

namespace GBMS.Services;

public static class ApplicationFactory
{
    private static SimulationManager? _simulationManager;

    private static Messenger? _messenger;

    private static RouteManager? _routeManager;

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

        _simulationManager = new SimulationManager();

        var persistence = new RoutePersistence();
        _routeManager = new RouteManager(persistence);
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

    public static RouteManager RouteManager =>
    _routeManager ?? throw new InvalidOperationException(
        "ApplicationFactory has not been initialized.");

}