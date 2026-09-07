using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GBMS.Models;

namespace GBMS.Services;

/// <summary>
/// Loads vehicle type definitions from JSON files in the
/// application-specific writable data directory. One file per type,
/// named after the type's VehicleTypeId - see
/// GBMS_Vehicle_Configuration_Design.md §3.
/// </summary>
public class VehicleTypePersistence : IVehicleTypePersistence
{
    private readonly string _vehicleTypesDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public VehicleTypePersistence(string? applicationDataDirectory = null)
    {
        applicationDataDirectory ??=
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);

        _vehicleTypesDirectory = Path.Combine(
            applicationDataDirectory,
            "GBMS",
            "VehicleTypes");

        Directory.CreateDirectory(_vehicleTypesDirectory);

        Logger.Debug($"VehicleTypePersistence initialized. VehicleTypes directory: {_vehicleTypesDirectory}");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Loads all valid vehicle type files from the VehicleTypes directory.
    /// Invalid files are ignored and logged.
    /// </summary>
    public IReadOnlyList<VehicleType> LoadAll()
    {
        var vehicleTypes = new List<VehicleType>();

        foreach (string filePath in Directory.EnumerateFiles(
                     _vehicleTypesDirectory,
                     "*.json"))
        {
            if (TryLoad(filePath, out VehicleType? vehicleType))
            {
                vehicleTypes.Add(vehicleType!);
            }
        }

        Logger.Debug($"Loaded {vehicleTypes.Count} vehicle type(s) from vehicle type library.");

        return vehicleTypes
            .OrderBy(vehicleType => vehicleType.VehicleTypeId)
            .ToList();
    }

    private bool TryLoad(string filePath, out VehicleType? vehicleType)
    {
        vehicleType = null;

        try
        {
            string json = File.ReadAllText(filePath);

            VehicleType? loaded =
                JsonSerializer.Deserialize<VehicleType>(json, _jsonOptions);

            if (loaded == null)
            {
                return false;
            }

            string fileId = Path.GetFileNameWithoutExtension(filePath);

            if (!Validate(loaded, fileId, out string error))
            {
                Logger.Warning(
                    "Ignoring invalid vehicle type file '{FilePath}': {Error}",
                    filePath, error);
                return false;
            }

            vehicleType = loaded;
            return true;
        }
        catch (JsonException ex)
        {
            Logger.Warning("Ignoring vehicle type file '{FilePath}': {Message}", filePath, ex.Message);
            return false;
        }
        catch (IOException ex)
        {
            Logger.Warning("Ignoring vehicle type file '{FilePath}': {Message}", filePath, ex.Message);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Warning("Ignoring vehicle type file '{FilePath}': {Message}", filePath, ex.Message);
            return false;
        }
    }

    private static bool Validate(VehicleType vehicleType, string fileId, out string error)
    {
        if (string.IsNullOrWhiteSpace(vehicleType.VehicleTypeId))
        {
            error = "VehicleTypeId is required.";
            return false;
        }

        if (vehicleType.VehicleTypeId != fileId)
        {
            error = $"VehicleTypeId '{vehicleType.VehicleTypeId}' does not match filename '{fileId}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(vehicleType.DisplayName))
        {
            error = "DisplayName is required.";
            return false;
        }

        if (!IsFiniteAndNonNegative(vehicleType.MinSpeedKmh) ||
            !IsFiniteAndNonNegative(vehicleType.MaxSpeedKmh) ||
            vehicleType.MaxSpeedKmh < vehicleType.MinSpeedKmh)
        {
            error = "MinSpeedKmh/MaxSpeedKmh must be non-negative, finite, and Max must not be less than Min.";
            return false;
        }

        if (!IsFiniteAndPositive(vehicleType.MaxTurnRateDegreesPerSecond))
        {
            error = "MaxTurnRateDegreesPerSecond must be a positive, finite value.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsFiniteAndNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0;

    private static bool IsFiniteAndPositive(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
}