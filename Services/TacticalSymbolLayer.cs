using System;
using System.IO;
using System.Text;
using GBMS.Models;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;

namespace GBMS.Services;

public sealed class TacticalSymbolLayer
{
    private const string LayerName = "Tactical Symbols";

    // Initial display scale. We can tune this once the actual
    // APP-6 symbol is displayed on the map.
    private const double SymbolScale = 0.05;

    private readonly MemoryLayer _layer;

    public TacticalSymbolLayer()
    {
        _layer = new MemoryLayer(LayerName)
        {
            Features = Array.Empty<IFeature>(),
            Style = null
        };
    }

    public MemoryLayer Layer => _layer;

    public void SetOwnVehicle(OwnVehicle ownVehicle)
    {
        var position = SphericalMercator.FromLonLat(
            ownVehicle.Position.Longitude,
            ownVehicle.Position.Latitude);

        var feature = new PointFeature(new MPoint(position.x, position.y));

        feature.Styles.Add(
            CreateSymbolStyle(
                "FriendlyTrackedArmouredVehicle",
                ownVehicle.Position.Heading));

        _layer.Features = new[] { feature };

        _layer.FeaturesWereModified();
    }

    private static ImageStyle CreateSymbolStyle(
        string symbolId,
        double heading)
    {
        using var stream =
            TacticalSymbolResolver.Resolve(symbolId);

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8);

        var svgContent = reader.ReadToEnd();

        return new ImageStyle
        {
            Image = new Image
            {
                Source = $"svg-content://{svgContent}"
            },

            SymbolScale = SymbolScale,

            // Heading is clockwise from North:
            //   0   = North
            //   90  = East
            //   180 = South
            //   270 = West
            SymbolRotation = heading,

            // The symbol is positioned at the centre of
            // the PointFeature.
            RelativeOffset = new RelativeOffset(0, 0),

            // The vehicle heading is map-relative.
            RotateWithMap = true
        };
    }
}