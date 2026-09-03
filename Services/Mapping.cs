using System;
using System.Collections.Generic;
using Mapsui;
using Mapsui.Projections;

namespace GBMS.Services;

/// <summary>
/// Provides mapping-related utility functions.
/// </summary>
public class Mapping
{
    /// <summary>
    /// The number of points used to approximate a geodesic circle.
    /// </summary>
    private const int GeodesicPointCount = 72;

    /// <summary>
    /// The radius of the Earth in kilometers.
    /// </summary>
    private const double EarthRadiusKm = 6371.0;

    public static double EarthRadiusKm1 => EarthRadiusKm;

    /// <summary>
    /// The maximum latitude value for the Web Mercator projection.
    /// </summary>
    private const double WebMercatorLatitudeLimit = 85.05112878;

    /// <summary>
    /// The approximate number of kilometers in one degree of latitude.
    /// </summary>
    private const double KMInOneDegreeLat = 111.0;

    public static double KMInOneDegreeLat1 => KMInOneDegreeLat;

    /// <summary>
    /// Creates a list of points representing a geodesic circle.
    /// This method approximates a geodesic circle by calculating points at equal 
    /// angular intervals around the center point.
    /// </summary>
    /// <param name="latitude">The latitude of the center point.</param>
    /// <param name="longitude">The longitude of the center point.</param>
    /// <param name="radiusKm">The radius of the circle in kilometers.</param>
    /// <returns>A list of points representing the geodesic circle.</returns>
    public static List<MPoint> CreateGeodesicCircle(double latitude, double longitude, double radiusKm)
    {
        var points = new List<MPoint>(GeodesicPointCount);

        // Convert the POI position to radians.
        double latitudeRadians = latitude * Math.PI / 180.0;
        double longitudeRadians = longitude * Math.PI / 180.0;

        // Angular distance represented by the radius.
        double angularDistance = radiusKm / EarthRadiusKm;

        for (int i = 0; i < GeodesicPointCount; i++)
        {
            // Bearing around the circle.
            double bearing = 2.0 * Math.PI * i / GeodesicPointCount;

            // Calculate latitude at this bearing and distance.
            double pointLatitude = Math.Asin(
                Math.Sin(latitudeRadians) *
                    Math.Cos(angularDistance)
                +
                Math.Cos(latitudeRadians) *
                    Math.Sin(angularDistance) *
                    Math.Cos(bearing));

            // Calculate longitude at this bearing and distance.
            double pointLongitude =
                longitudeRadians +
                Math.Atan2(
                    Math.Sin(bearing) *
                        Math.Sin(angularDistance) *
                        Math.Cos(latitudeRadians),
                    Math.Cos(angularDistance) -
                        Math.Sin(latitudeRadians) *
                        Math.Sin(pointLatitude));

            // Convert back to degrees.
            double pointLatitudeDegrees = pointLatitude * 180.0 / Math.PI;

            double pointLongitudeDegrees = pointLongitude * 180.0 / Math.PI;

            // Web Mercator cannot represent the poles.
            pointLatitudeDegrees = Math.Clamp(
                pointLatitudeDegrees,
                -WebMercatorLatitudeLimit,
                WebMercatorLatitudeLimit);

            // Convert geographic coordinates to
            // Spherical Mercator coordinates used by Mapsui.
            var projected = SphericalMercator.FromLonLat(pointLongitudeDegrees, pointLatitudeDegrees);

            points.Add(new MPoint(projected.x, projected.y));
        }

        return points;
    }

    /// <summary>
    /// Calculates the distance between two geographic coordinates using the Haversine formula.
    /// This method assumes the Earth is a perfect sphere, which is a reasonable approximation for small distances.
    /// </summary>
    /// <param name="latitude1">The latitude of the first point in degrees.</param>
    /// <param name="longitude1">The longitude of the first point in degrees.</param>
    /// <param name="latitude2">The latitude of the second point in degrees.</param>
    /// <param name="longitude2">The longitude of the second point in degrees.</param>
    /// <returns>The distance between the two points in kilometers.</returns>
    public static double CalculateDistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKm = 6371.0;

        double lat1 = latitude1 * Math.PI / 180.0;
        double lat2 = latitude2 * Math.PI / 180.0;

        double deltaLat = (latitude2 - latitude1) * Math.PI / 180.0;
        double deltaLon = (longitude2 - longitude1) * Math.PI / 180.0;

        double a =
            Math.Sin(deltaLat / 2.0) * Math.Sin(deltaLat / 2.0)
            +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Sin(deltaLon / 2.0) * Math.Sin(deltaLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return earthRadiusKm * c;
    }

    /// <summary>
    /// Calculates the map extent (bounding box) around a given geographic coordinate with a specified extent in kilometers.
    /// </summary>
    /// <param name="latitude">The latitude of the center point in degrees.</param>
    /// <param name="longitude">The longitude of the center point in degrees.</param>
    /// <param name="extentKm">The extent around the center point in kilometers.</param>
    /// <returns>An <see cref="MRect"/> representing the bounding box in Spherical Mercator coordinates.</returns>
    public static MRect CalculateMapExtent(double latitude, double longitude, double extentKm)
    {
        double latitudeOffset = extentKm / KMInOneDegreeLat;

        double longitudeOffset =
            extentKm / (KMInOneDegreeLat * Math.Cos(latitude * Math.PI / 180.0));

        double minLatitude = latitude - latitudeOffset;
        double maxLatitude = latitude + latitudeOffset;

        double minLongitude = longitude - longitudeOffset;
        double maxLongitude = longitude + longitudeOffset;

        var min = SphericalMercator.FromLonLat(
            minLongitude, minLatitude);

        var max = SphericalMercator.FromLonLat(
            maxLongitude, maxLatitude);

        var box = new MRect(
            min.x, min.y, max.x, max.y);

        return box;
    }
}
