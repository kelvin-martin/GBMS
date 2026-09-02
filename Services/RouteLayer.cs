using System;
using System.Collections.Generic;
using GBMS.Models;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using NetTopologySuite.Geometries;


namespace GBMS.Services;

public sealed class RouteLayer
{
    private const string LayerName = "Route";

    private readonly MemoryLayer _layer;

    public RouteLayer()
    {
        _layer = new MemoryLayer(LayerName)
        {
            Features = Array.Empty<IFeature>(),
            Style = null
        };
    }

    public MemoryLayer Layer => _layer;


    private static LabelStyle CreateWaypointNumberStyle(int number)
    {
        return new LabelStyle
        {
            Text = number.ToString(),

            Font = new Font
            {
                FontFamily = "Arial",
                Size = 14
            },

            ForeColor = Color.Black,

            // Position the number above the waypoint.
            Offset = new Offset
            {
                X = 0,
                Y = -20
            },

            HorizontalAlignment =
                LabelStyle.HorizontalAlignmentEnum.Center,

            VerticalAlignment =
                LabelStyle.VerticalAlignmentEnum.Center,

            // White halo makes the number readable against the map.
            Halo = new Pen
            {
                Color = Color.White,
                Width = 3
            },

            BackColor = null,
            CollisionDetection = false
        };
    }

    private static VectorStyle CreateRouteLineStyle()
    {
        return new VectorStyle
        {
            Line = new Pen(Color.Red, 2)
        };
    }

    public void SetRoute(Route route)
    {
        var features = new List<IFeature>();

        // Add route line.
        if (route.Waypoints.Count >= 2)
        {
            var coordinates = new Coordinate[route.Waypoints.Count];

            for (int i = 0; i < route.Waypoints.Count; i++)
            {
                var waypoint = route.Waypoints[i];

                var position = SphericalMercator.FromLonLat(
                    waypoint.Longitude,
                    waypoint.Latitude);

                coordinates[i] = new Coordinate(
                    position.x,
                    position.y);
            }

            var lineString = new LineString(coordinates);

            var lineFeature = new GeometryFeature(lineString);

            lineFeature.Styles.Add(CreateRouteLineStyle());

            features.Add(lineFeature);
        }

        // Add waypoint markers and numbers.
        for (int i = 0; i < route.Waypoints.Count; i++)
        {
            var waypoint = route.Waypoints[i];

            var position = SphericalMercator.FromLonLat(
                waypoint.Longitude,
                waypoint.Latitude);

            var feature = new PointFeature(
                new MPoint(position.x, position.y));

            feature.Styles.Add(CreateWaypointStyle());
            feature.Styles.Add(CreateWaypointNumberStyle(i + 1));

            features.Add(feature);
        }

        _layer.Features = features;
        _layer.FeaturesWereModified();
    }

    public void Clear()
    {
        _layer.Features = Array.Empty<IFeature>();
        _layer.FeaturesWereModified();
    }

    private static SymbolStyle CreateWaypointStyle()
    {
        return new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = 0.5,
            Fill = new Brush(Color.Red),
            Outline = new Pen(Color.White, 2)
        };
    }
}