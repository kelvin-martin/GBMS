using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Widgets;
using Mapsui.Widgets.InfoWidgets;

namespace GBMS.Views.Mfd;

/// <summary>
/// A widget that displays the latitude and longitude of the mouse pointer.
/// Note: The coordinates are displayed in the WGS84 coordinate system (EPSG:4326).
/// The default coordinate system of Mapsui is Spherical Mercator (EPSG:3857), 
/// so the coordinates are converted to WGS84 before being displayed.
/// </summary>
public class LatLonMouseCoordinatesWidget : MouseCoordinatesWidget
{

    public override void OnPointerMoved(WidgetEventArgs e)
    {
        var worldPosition = e.Map.Navigator.Viewport.ScreenToWorld(e.ScreenPosition);

        var lonLat = SphericalMercator.ToLonLat(worldPosition.X, worldPosition.Y);

        Text = $"Lat: {lonLat.lat:F5}, Lon: {lonLat.lon:F5}";

        e.Map.RefreshGraphics();
    }

}
