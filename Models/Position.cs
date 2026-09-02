
namespace GBMS.Models;

/// <summary>
/// represents a geographic position with latitude, longitude, and heading.
/// </summary>
/// <param name="Latitude">the latitude of the position, in degrees</param>
/// <param name="Longitude">the longitude of the position, in degrees</param>
/// <param name="Heading">the heading of the position, in degrees</param>
public record Position( double Latitude, double Longitude, double Heading);
