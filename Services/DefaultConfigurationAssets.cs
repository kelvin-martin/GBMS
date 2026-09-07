using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia.Platform;
using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Loads the built-in default vehicle type and default scenario, bundled
/// as Avalonia assets so they are always present regardless of the
/// writable data directory's state. Used as a fallback when the vehicle
/// type library or scenario file is missing, empty, or invalid - see
/// GBMS_Vehicle_Configuration_Design.md §5.
/// </summary>
public static class DefaultConfigurationAssets
{
    private const string VehicleTypeUri = "avares://GBMS/Assets/Configuration/DefaultVehicleType.json";
    private const string ScenarioUri = "avares://GBMS/Assets/Configuration/DefaultScenario.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Loads the built-in default vehicle type.
    /// </summary>
    public static VehicleType LoadVehicleType()
    {
        return Load<VehicleType>(VehicleTypeUri);
    }

    /// <summary>
    /// Loads the built-in default scenario.
    /// </summary>
    public static ScenarioConfiguration LoadScenario()
    {
        return Load<ScenarioConfiguration>(ScenarioUri);
    }

    private static T Load<T>(string assetUri)
    {
        var uri = new Uri(assetUri);

        if (!AssetLoader.Exists(uri))
        {
            // Bundled with the application - absence here is a
            // build/packaging defect, not a user configuration problem,
            // so this throws rather than degrading gracefully.
            throw new FileNotFoundException(
                $"Default configuration asset '{assetUri}' was not found. This is a build/packaging error.",
                assetUri);
        }

        using Stream stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string json = reader.ReadToEnd();

        T? value = JsonSerializer.Deserialize<T>(json, JsonOptions);

        if (value == null)
        {
            throw new InvalidOperationException(
                $"Default configuration asset '{assetUri}' deserialized to null. This is a build/packaging error.");
        }

        return value;
    }
}