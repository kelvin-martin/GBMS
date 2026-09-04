

using System;
using System.Collections.Generic;

namespace GBMS.Models;

public class Route
{
    public int Id { get; set; }

    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }

    public List<Waypoint> Waypoints { get; set; } = new();
}
