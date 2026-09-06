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

    private static Color RouteColor(bool isAssigned)
    {
        return isAssigned ? Color.Blue : Color.Red;
    }

    private static VectorStyle CreateRouteLineStyle(bool isAssigned)
    {
        return new VectorStyle
        {
            Line = new Pen(RouteColor(isAssigned), 2)
        };
    }

    /// <summary>
    /// Sets the complete presentation state for this layer: the route to draw (or
    /// null to clear it) and whether it should be rendered as the route currently
    /// assigned to Own Vehicle. This layer holds no state between calls - callers
    /// must pass the full state every time.
    /// </summary>
    public void SetRoute(Route? route, bool isAssigned)
    {
        if (route == null)
        {
            Clear();
            return;
        }

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

            lineFeature.Styles.Add(CreateRouteLineStyle(isAssigned));

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

            feature.Styles.Add(CreateWaypointStyle(isAssigned));
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

    private static SymbolStyle CreateWaypointStyle(bool isAssigned)
    {
        return new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = 0.5,
            Fill = new Brush(RouteColor(isAssigned)),
            Outline = new Pen(Color.White, 2)
        };
    }
}