using System;
using System.Collections.Generic;
using System.Linq;
using GBMS.Models;

namespace GBMS.Services;

public class RouteManager
{
    private readonly IRoutePersistence _persistence;
    private readonly List<Route> _routes = new();

    /// <summary>
    /// Raised when the collection of available routes changes.
    /// </summary>
    public event Action? RoutesChanged;

    /// <summary>
    /// Raised when the current/displayed route changes.
    /// </summary>
    public event Action<Route?>? CurrentRouteChanged;

    /// <summary>
    /// Gets all routes currently available to the application.
    /// </summary>
    public IReadOnlyList<Route> Routes => _routes;

    /// <summary>
    /// Gets the route currently displayed on the Tactical Map.
    /// </summary>
    public Route? CurrentRoute { get; private set; }

    public RouteManager(IRoutePersistence persistence)
    {
        ArgumentNullException.ThrowIfNull(persistence);

        _persistence = persistence;

        LoadRoutes();
    }

    /// <summary>
    /// Loads all persisted routes into the Route Manager.
    /// </summary>
    private void LoadRoutes()
    {
        _routes.Clear();

        IReadOnlyList<Route> routes = _persistence.LoadAll();

        _routes.AddRange(routes);

        Logger.Debug($"Loaded {_routes.Count} routes from persistent storage.");

        CurrentRoute = null;
    }

    /// <summary>
    /// Selects a route for display on the Tactical Map.
    /// </summary>
    public void SelectRoute(int routeId)
    {
        Route? route = _routes.FirstOrDefault(
            r => r.Id == routeId);

        if (route == null)
        {
            throw new InvalidOperationException(
                $"Route {routeId} does not exist.");
        }

        if (ReferenceEquals(CurrentRoute, route))
        {
            return;
        }

        CurrentRoute = route;

        CurrentRouteChanged?.Invoke(CurrentRoute);
    }

    /// <summary>
    /// Clears the currently selected/displayed route.
    /// </summary>
    public void ClearCurrentRoute()
    {
        if (CurrentRoute == null)
        {
            return;
        }

        CurrentRoute = null;

        CurrentRouteChanged?.Invoke(null);
    }

    /// <summary>
    /// Adds a new route to the Route Manager and persists it.
    /// </summary>
    public void AddRoute(Route route)
    {
        if (route.Id == 0)
        {
            _persistence.SaveNew(route);
            _routes.Add(route);
        }
        else
        {
            route.Modified = DateTime.UtcNow;
            _persistence.Save(route);
        }

        RoutesChanged?.Invoke();
    }

    /// <summary>
    /// Saves changes to an existing route.
    /// </summary>
    public void SaveRoute(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (!_routes.Contains(route))
        {
            throw new InvalidOperationException(
                $"Route {route.Id} is not managed by the Route Manager.");
        }

        _persistence.Save(route);
    }

    /// <summary>
    /// Removes a route from the Route Manager and persistent storage.
    /// </summary>
    public void RemoveRoute(int routeId)
    {
        Route? route = _routes.FirstOrDefault(
            r => r.Id == routeId);

        if (route == null)
        {
            throw new InvalidOperationException(
                $"Route {routeId} does not exist.");
        }

        if (ReferenceEquals(CurrentRoute, route))
        {
            throw new InvalidOperationException(
                $"Route {routeId} is currently displayed and cannot be removed.");
        }

        _persistence.Delete(route);

        _routes.Remove(route);

        RoutesChanged?.Invoke();
    }
}
