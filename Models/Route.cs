

using System;
using System.Collections.Generic;

namespace GBMS.Models;

public class Route
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }

    public List<Waypoint> Waypoints { get; set; } = new();
}
