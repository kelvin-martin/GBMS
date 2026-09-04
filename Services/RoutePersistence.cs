using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Provides persistence of routes using JSON files in the
/// application-specific writable data directory.
/// </summary>
public class RoutePersistence : IRoutePersistence
{
    private const int CurrentFileVersion = 1;

    private readonly string _routesDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public RoutePersistence(string? applicationDataDirectory = null)
    {
        applicationDataDirectory ??=
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);

        _routesDirectory = Path.Combine(
            applicationDataDirectory,
            "GBMS",
            "Routes");

        Directory.CreateDirectory(_routesDirectory);

        Logger.Debug($"RoutePersistence initialized. Routes directory: {_routesDirectory}");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Loads all valid route files from the Routes directory.
    /// Invalid files are ignored.
    /// </summary>
    public IReadOnlyList<Route> LoadAll()
    {
        var routes = new List<Route>();

        foreach (string filePath in Directory.EnumerateFiles(
                     _routesDirectory,
                     "*.json"))
        {
            if (TryLoad(filePath, out Route? route))
            {
                routes.Add(route!);
            }
        }

        return routes
            .OrderBy(route => route.Id)
            .ToList();
    }

    /// <summary>
    /// Assigns the next available route ID and persists the route.
    /// </summary>
    public void SaveNew(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (route.Id != 0)
        {
            throw new InvalidOperationException(
                "A new route must have an ID of 0.");
        }

        if (!ValidateRoute(route, out string error))
        {
            throw new InvalidOperationException(
                $"Cannot save route: {error}");
        }

        route.Id = GetNextRouteId();

        Save(route);
    }

    /// <summary>
    /// Saves an existing route to its corresponding JSON file.
    /// </summary>
    public void Save(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (route.Id <= 0)
        {
            throw new InvalidOperationException(
                "A persisted route must have an ID greater than zero.");
        }

        if (!ValidateRoute(route, out string error))
        {
            throw new InvalidOperationException(
                $"Cannot save route {route.Id}: {error}");
        }

        route.Modified = DateTime.UtcNow;

        string filePath = GetRouteFilePath(route.Id);

        string json = JsonSerializer.Serialize(
            new PersistedRoute
            {
                Version = CurrentFileVersion,
                Id = route.Id,
                Created = route.Created,
                Modified = route.Modified,
                Waypoints = route.Waypoints
            },
            _jsonOptions);

        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Deletes the JSON file associated with a persisted route.
    /// </summary>
    public void Delete(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (route.Id <= 0)
        {
            throw new InvalidOperationException(
                "A persisted route must have an ID greater than zero.");
        }

        string filePath = GetRouteFilePath(route.Id);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private bool TryLoad(string filePath, out Route? route)
    {
        route = null;

        try
        {
            string json = File.ReadAllText(filePath);

            PersistedRoute? persistedRoute =
                JsonSerializer.Deserialize<PersistedRoute>(
                    json,
                    _jsonOptions);

            if (persistedRoute == null)
            {
                return false;
            }

            if (!int.TryParse(
                    Path.GetFileNameWithoutExtension(filePath),
                    out int fileId))
            {
                return false;
            }

            if (!ValidatePersistedRoute(
                    persistedRoute,
                    fileId))
            {
                return false;
            }

            route = new Route
            {
                Id = persistedRoute.Id,
                Created = persistedRoute.Created,
                Modified = persistedRoute.Modified,
                Waypoints = persistedRoute.Waypoints
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool ValidatePersistedRoute(PersistedRoute route, int fileId)
    {
        if (route.Version != CurrentFileVersion)
        {
            return false;
        }

        if (route.Id <= 0)
        {
            return false;
        }

        if (route.Id != fileId)
        {
            return false;
        }

        if (route.Created == DateTime.MinValue)
        {
            return false;
        }

        if (route.Modified == DateTime.MinValue)
        {   
            return false; 
        }

        if (route.Waypoints == null ||
            route.Waypoints.Count < 2)
        {
            return false;
        }

        return ValidateWaypoints(route.Waypoints);
    }

    private bool ValidateRoute(Route route, out string error)
    {
        if (route.Waypoints == null ||
            route.Waypoints.Count < 2)
        {
            error = "A route must contain at least two waypoints.";
            return false;
        }

        if (!ValidateWaypoints(route.Waypoints))
        {
            error = "One or more waypoints contain invalid values.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateWaypoints(
        IEnumerable<Waypoint> waypoints)
    {
        foreach (Waypoint waypoint in waypoints)
        {
            if (double.IsNaN(waypoint.Latitude) ||
                double.IsInfinity(waypoint.Latitude) ||
                waypoint.Latitude < -90.0 ||
                waypoint.Latitude > 90.0)
            {
                return false;
            }

            if (double.IsNaN(waypoint.Longitude) ||
                double.IsInfinity(waypoint.Longitude) ||
                waypoint.Longitude < -180.0 ||
                waypoint.Longitude > 180.0)
            {
                return false;
            }

            if (double.IsNaN(waypoint.Speed) ||
                double.IsInfinity(waypoint.Speed) ||
                waypoint.Speed < 0.0)
            {
                return false;
            }
        }

        return true;
    }

    private int GetNextRouteId()
    {
        int highestId = 0;

        foreach (string filePath in Directory.EnumerateFiles(
                     _routesDirectory,
                     "*.json"))
        {
            string fileName =
                Path.GetFileNameWithoutExtension(filePath);

            if (int.TryParse(fileName, out int id) &&
                id > highestId)
            {
                highestId = id;
            }
        }

        return highestId + 1;
    }

    private string GetRouteFilePath(int id)
    {
        return Path.Combine(
            _routesDirectory,
            $"{id:D3}.json");
    }

    /// <summary>
    /// Representation of the JSON route file.
    /// </summary>
    private sealed class PersistedRoute
    {
        public int Version { get; set; }

        public int Id { get; set; }

        public DateTime Created { get; set; }

        public DateTime Modified { get; set; }

        public List<Waypoint> Waypoints { get; set; } = new();
    }
}