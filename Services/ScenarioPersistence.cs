using System;
using System.IO;
using System.Text.Json;
using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Loads the scenario configuration from a single JSON file in the
/// application-specific writable data directory. See
/// GBMS_Vehicle_Configuration_Design.md §4.
/// </summary>
public class ScenarioPersistence : IScenarioPersistence
{
    private readonly string _scenarioFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ScenarioPersistence(string? applicationDataDirectory = null)
    {
        applicationDataDirectory ??=
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);

        _scenarioFilePath = Path.Combine(
            applicationDataDirectory,
            "GBMS",
            "Scenario.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public ScenarioConfiguration? Load()
    {
        if (!File.Exists(_scenarioFilePath))
        {
            Logger.Debug($"No scenario file found at: {_scenarioFilePath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(_scenarioFilePath);

            ScenarioConfiguration? scenario =
                JsonSerializer.Deserialize<ScenarioConfiguration>(json, _jsonOptions);

            if (scenario == null)
            {
                Logger.Warning(
                    "Ignoring invalid scenario file '{FilePath}': empty or malformed.",
                    _scenarioFilePath);
                return null;
            }

            if (!Validate(scenario, out string error))
            {
                Logger.Warning(
                    "Ignoring invalid scenario file '{FilePath}': {Error}",
                    _scenarioFilePath, error);
                return null;
            }

            return scenario;
        }
        catch (JsonException ex)
        {
            Logger.Warning("Ignoring scenario file '{FilePath}': {Message}", _scenarioFilePath, ex.Message);
            return null;
        }
        catch (IOException ex)
        {
            Logger.Warning("Ignoring scenario file '{FilePath}': {Message}", _scenarioFilePath, ex.Message);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Warning("Ignoring scenario file '{FilePath}': {Message}", _scenarioFilePath, ex.Message);
            return null;
        }
    }
    

    private static bool Validate(ScenarioConfiguration scenario, out string error)
    {
        if (string.IsNullOrWhiteSpace(scenario.VehicleTypeId))
        {
            error = "VehicleTypeId is required.";
            return false;
        }

        if (double.IsNaN(scenario.StartLatitude) || double.IsInfinity(scenario.StartLatitude) ||
            scenario.StartLatitude < -90.0 || scenario.StartLatitude > 90.0)
        {
            error = "StartLatitude must be a valid latitude.";
            return false;
        }

        if (double.IsNaN(scenario.StartLongitude) || double.IsInfinity(scenario.StartLongitude) ||
            scenario.StartLongitude < -180.0 || scenario.StartLongitude > 180.0)
        {
            error = "StartLongitude must be a valid longitude.";
            return false;
        }

        if (double.IsNaN(scenario.StartHeading) || double.IsInfinity(scenario.StartHeading) ||
            scenario.StartHeading < 0.0 || scenario.StartHeading >= 360.0)
        {
            error = "StartHeading must be between 0 and 360 degrees.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}