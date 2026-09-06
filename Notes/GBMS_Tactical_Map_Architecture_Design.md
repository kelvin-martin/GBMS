# GBMS Tactical Map — Architecture Design

**Status:** MVP architecture baseline  
**Scope:** Tactical Map presentation, Own Vehicle, route display, Route Editor, Route Manager, MFD interaction, map interaction, UI conventions, and agreed implementation constraints.

---

## 1. Core Services

### Tactical Map presentation
- `TacticalMapView` owns the Tactical Map presentation and Mapsui `MapControl`.
- Current map layers:
  - `TacticalSymbolLayer` — Own Vehicle tactical symbol.
  - `RouteLayer` — currently displayed route.
  - `MemoryLayer` — selected-waypoint highlight during route editing.
- `TacticalMapView` owns `RouteEditor` and `RouteManagerControl`.
- The map uses an online OpenStreetMap tile layer for development; deployment is intended to use a local tile cache.
- Map operations are performed by the view in response to ViewModel events/requests. The ViewModel does not manipulate Mapsui directly.

### SimulationClock / SimulationLoop
- `SimulationClock` provides simulation time.
- `SimulationLoop` drives simulation updates independently of UI presentation.
- Tactical-map simulation updates arrive through `TacticalMapViewModel.MapUpdateRequested`.
- `TacticalMapView` responds by updating the Own Vehicle symbol and calling `MapControl.RefreshGraphics()`.
- UI refresh frequency is deliberately decoupled from simulation/rendering frequency.

### RouteManager
- `RouteManager` is the authoritative owner of:
  - route collection;
  - `CurrentRoute`;
  - route selection;
  - route addition/removal;
  - route persistence.
- `CurrentRouteChanged` is the presentation boundary for a newly selected route.
- `RoutesChanged` allows route-list presentation to refresh.
- Route Manager knows nothing about MFD buttons, cursor/scrolling presentation, or map rendering.
- Also owns assignment of a route to Own Vehicle (`AssignedRoute`, `AssignRoute`, `AssignedRouteChanged`) — see `GBMS_Route_Assignment_Design.md`.

### RouteEditor
- `RouteEditor` is a lightweight service owned by `TacticalMapView`.
- It manages the route currently being created/edited on the Tactical Map.
- It owns temporary/current editing route, creation-mode state, selected waypoint, dragged waypoint, waypoint addition/movement, waypoint speed adjustment, and waypoint deletion.
- It has no knowledge of `RouteManager`.
- A temporary route is retained when leaving creation mode; re-entering creation resumes editing that route.
- Route editing is deliberately separate from route management.

#### Waypoint deletion (implemented)
- `RouteEditor.CanDeleteSelectedWaypoint` — true only when a waypoint is selected and the route has more than the minimum waypoint count (`MinimumWaypointCount = 2`, matching `RoutePersistence`'s save-time minimum).
- `RouteEditor.DeleteSelectedWaypoint()` — removes the selected waypoint, clears `_selectedWaypointIndex` and `_draggedWaypointIndex` (rather than attempting to preserve/shift them), and returns whether the deletion occurred.
- The minimum-count guard lives in `RouteEditor`, not at save time — consistent with validating destructive actions at the point of the action rather than downstream, the same principle already applied to `RouteManager`'s deletion guards.
- Deletion does **not** require the route to be unassigned. A waypoint on the currently-assigned route can be deleted at any time, including the waypoint Own Vehicle is currently navigating to — see `GBMS_Route_Assignment_Navigation_Design.md` §1 for how `OwnVehicle` stays correct when this happens.
- Selection is simply cleared on delete, not reassigned to an adjacent waypoint — simplest option, consistent with how selection is already cleared elsewhere (right-click, `SetRoute`).

### RouteLayer
- `RouteLayer` renders the route supplied by `TacticalMapView`.
- `TacticalMapView` calls `SetRoute(...)` when the displayed route changes or when an edited route changes.
- Rendering remains separate from route ownership and editing.

### TacticalSymbolLayer / Own Vehicle
- `TacticalSymbolLayer` renders the Own Vehicle tactical symbol.
- `TacticalMapView.UpdateOwnVehicleSymbol()` reads `TacticalMapViewModel.OwnVehicleState` and passes it to the layer.
- Simulation-driven map updates explicitly refresh graphics.
- L1 centres the map on the current Own Vehicle position.

### Mapping / projection
- Mapsui Spherical Mercator is the map-coordinate boundary.
- Geographic latitude/longitude is converted to/from Spherical Mercator for map interaction.
- Map panning is expressed as ground distance and converted to latitude/longitude offsets; longitude distance accounts for latitude.
- Map extents use the existing mapping utilities and `ZoomToBox(..., MBoxFit.Fit)`.
- Geodesic circles are used for geographic selection/radius presentation where required.
- Waypoint hit-testing for map-pointer selection is **screen-space**, not ground-space — see §3, "Route editing pointer interaction" below. `Mapping.CalculateDistanceKm` remains ground-space and is still used elsewhere (e.g. `OwnVehicle`'s navigation distance calculations); it is no longer used for pointer hit-testing.

---

## 2. Data Models

### Route
A route is the domain object representing an ordered collection of waypoints.

Route Manager presentation currently exposes:
- `Id`;
- `Name`;
- `Created`;
- `Modified`;
- waypoint count;
- first-waypoint position.

Route Manager display ordering:
1. `Modified` descending;
2. `Id` descending.

### Waypoint
A waypoint contains route-navigation data including:
- latitude;
- longitude;
- speed.

Route Editor MVP speed limits:
- default: 30;
- minimum: 10;
- maximum: 70.

Route Editor MVP waypoint count limit:
- minimum: 2 (enforced by both `RoutePersistence` at save time and `RouteEditor.CanDeleteSelectedWaypoint` at delete time).

### Own Vehicle state
- Own Vehicle state is supplied to the Tactical Map through `TacticalMapViewModel.OwnVehicleState`.
- The map does not own simulation state.
- The tactical symbol is updated from the current state supplied by the ViewModel.

### Route Manager presentation item
`RouteDisplayItem` is a deliberately lightweight wrapper around a `Route`.
It exposes:
- `RouteText`;
- `ModifiedText`;
- `WaypointText`;
- `FirstWaypointText`;
- `SelectionIndicator`.

Only `SelectionIndicator` is dynamic UI state requiring change notification. `RouteDisplayItem` therefore implements `INotifyPropertyChanged` only for that property.

---

## 3. Interfaces

### TacticalMapViewModel → TacticalMapView
`TacticalMapViewModel` receives MFD input and exposes event/request boundaries for:
- centre map on Own Vehicle;
- zoom-level changes;
- map pan;
- simulation/map update;
- route creation request;
- contextual function-key requests.

The ViewModel does **not** own `RouteManagerControl` or `RouteEditor`.

### Fixed MFD map controls

| Button | Function |
|---|---|
| R1 | Previous zoom level |
| R2 | Next zoom level |
| R3 | Pan left |
| R4 | Pan right |
| R5 | Pan up |
| R6 | Pan down |

R1–R6 remain fixed map-view controls and are not contextual.

### Contextual MFD controls

| Button | Normal Tactical Map | Route Manager | Route Editing |
|---|---|---|---|
| L1 | Centre on Own Vehicle | Centre on Own Vehicle | Centre on Own Vehicle |
| L2 | Request route creation | Existing route-creation context | Existing route-creation context |
| L3 | Show/hide Route Manager | Show/hide Route Manager | Existing editor context |
| L4 | Assign `CurrentRoute` to Own Vehicle | Accept/select highlighted route | Delete selected waypoint |
| L5 | — | Move route cursor up | Increase selected waypoint speed |
| L6 | — | Move route cursor down | Decrease selected waypoint speed |

L4 in Route Editing is only enabled (icon shown) when `RouteEditor.CanDeleteSelectedWaypoint` is true — i.e. a waypoint is selected and the route has more than the minimum waypoint count. This follows the same "enabled only when meaningful" pattern already used for L4 in Normal Tactical Map (see `GBMS_Route_Assignment_Design.md` §3).

The established L5/L6 speed-edit mapping is the current implementation.

### Route Manager interaction
The intended route-management flow is:

```text
L3
  → TacticalMapView
  → RouteManagerControl.Show(...)
  → route collection visible

L5/L6
  → TacticalMapView
  → RouteManagerControl.MoveSelectionUp/Down()
  → local UI cursor changes

L4
  → TacticalMapView
  → RouteManagerControl.Select()
  → RouteManager.SelectRoute(id)
  → CurrentRouteChanged
  → TacticalMapView
  → RouteLayer.SetRoute(route)
  → MapControl.RefreshGraphics()
```

Critical distinction:
- L5/L6 change only the Route Manager **UI cursor**.
- L4 performs the genuine route selection.
- Cursor movement does not change `RouteManager.CurrentRoute`.

### Route Editing L4 interaction (waypoint deletion)

```text
L4 (Route Editing, waypoint selected, CanDeleteSelectedWaypoint true)
  → TacticalMapView.HandleWaypointEditFunctionKey
  → RouteEditor.DeleteSelectedWaypoint()
  → RouteLayer.SetRoute(...)
  → UpdateSelectedWaypointHighlight() / UpdateSelectedWaypointSpeedDisplay() / UpdateContextualIcons()
  → MapControl.RefreshGraphics()
```

If the deleted waypoint's route is currently assigned to Own Vehicle, no additional signalling occurs from `RouteEditor` or `TacticalMapView` — `OwnVehicle` observes the mutated `Route.Waypoints` list directly on its next `Update` tick, via the same shared-reference relationship already documented in `GBMS_Route_Assignment_Navigation_Design.md`.

### RouteManagerControl
`RouteManagerControl` obtains the application `RouteManager` from `ApplicationFactory.RouteManager`.

It owns:
- route-list presentation;
- local cursor/selection index;
- last-selected route ID;
- displayed/hidden state;
- cursor movement;
- selection forwarding;
- deletion forwarding.

It does not own:
- authoritative route domain data;
- map rendering;
- route editing;
- MFD input;
- map pointer interaction.

### CurrentRouteChanged → map
`TacticalMapView` independently references the same application `RouteManager` and subscribes to `CurrentRouteChanged`.

```text
RouteManager
     │
     └── CurrentRouteChanged
              │
              ▼
       TacticalMapView
              │
              ▼
       RouteLayer.SetRoute()
              │
              ▼
       MapControl.RefreshGraphics()
```

### Route editing pointer interaction
`TacticalMapView` owns Mapsui pointer interaction for route editing.

- Map tap in creation mode converts the Mapsui world position from Spherical Mercator to latitude/longitude.
- `RouteEditor.AddWaypoint(...)` creates the waypoint.
- The route layer is updated and graphics refreshed.
- Existing waypoints can be selected and dragged.
- During waypoint dragging, Mapsui panning is locked.
- On drag completion, normal Mapsui panning is restored and pointer capture is released.
- Route Manager is not involved in waypoint editing.

#### Waypoint hit-testing (revised — screen-space)

**Original approach (superseded):** waypoint selection used a fixed ground-distance radius (`FindWaypointAtPosition`, 50 m / 0.05 km), comparing the tap's lat/lon against each waypoint's lat/lon via `Mapping.CalculateDistanceKm`.

**Problem found:** a fixed ground-space radius doesn't scale with zoom. At high zoom, 50 m spans many screen pixels, making closely-spaced waypoints impossible to select individually. Shrinking the radius fixed that, but at low zoom the same 50 m collapses to a couple of screen pixels — clicks reliably missed the hit-test, fell through to `OnMapTapped`, and created a new waypoint instead of selecting the intended one.

**Fix:** hit-testing is done in **screen space**, matching the approach already used for the selected-waypoint highlight (a screen-space-sized halo, explicitly chosen over a fixed real-world radius so it stays a consistent size at any zoom level). `FindWaypointAtScreenPosition` converts each waypoint's world position to a screen position via `Viewport.WorldToScreen(...)` (returns `Mapsui.Manipulations.ScreenPosition` in the installed Mapsui version — not `MPoint`, which is a world-space type) and compares directly against the pointer's screen position using a fixed pixel radius (`WaypointHitRadiusPixels = 15.0`):

```csharp
private const double WaypointHitRadiusPixels = 15.0;

private int? FindWaypointAtScreenPosition(Avalonia.Point screenPosition)
{
    // Iterates CurrentRoute.Waypoints, converting each to screen space via
    // viewport.WorldToScreen(...), and returns the nearest waypoint within
    // WaypointHitRadiusPixels of screenPosition, or null if none match.
}
```

This single pixel-based threshold satisfies both original requirements at once: it tightens in ground terms when zoomed in (preserving discrimination between close waypoints) and widens in ground terms when zoomed out (keeping targets easy to hit) — without needing separate logic for either case.

The old `FindWaypointAtPosition` (ground-distance) method and its `Mapping.CalculateDistanceKm` call site have been removed from this flow; `Mapping.CalculateDistanceKm` remains in use elsewhere (e.g. `OwnVehicle`).

**Note on Mapsui API surface:** Mapsui has undergone notable API changes across recent major versions (particularly around gesture/manipulation types), and online documentation/examples frequently reflect older APIs. Where a new Mapsui call has no existing call site elsewhere in `TacticalMapView.axaml.cs` to pattern-match against, its signature should be confirmed against the actually-installed package version before relying on it.

---

## 4. UI Design

### Tactical Map composition

```text
TacticalMapView
├── Mapsui MapControl
│   ├── map tiles
│   ├── TacticalSymbolLayer
│   ├── RouteLayer
│   └── selected-waypoint layer
│
├── RouteManagerControl
└── RouteEditor presentation
```

`TacticalMapView` is the presentation coordinator.

### Route Manager overlay
- Route Manager is an overlay on the Tactical Map.
- It is not a mouse-driven list control.
- The pointer remains available for map-related interaction.
- MFD L5/L6 move the route cursor.
- L4 accepts the highlighted route.
- Displaying Route Manager does not enable route editing.
- Route Manager and Route Editor are mutually exclusive.

### Route Manager cursor
- `_selectedIndex` is local UI cursor state.
- `_lastSelectedRouteId` restores the cursor when the control is reopened.
- Opening the control restores the last selected route where possible; otherwise the first route is selected.
- Cursor movement is bounded at the first/last item.
- `SelectionIndicator` provides the visible `>` marker.
- Only the dynamic indicator requires property notification.

### Contextual icons
`UpdateContextualIcons()` derives visibility from existing state rather than introducing another state machine.

Normal map / Route Manager hidden:
- centre-vehicle icon visible;
- Route Manager icon visible;
- L5/L6 Route Manager cursor icons hidden;
- Accept icon hidden.

Route Manager displayed:
- Route Manager icon visible;
- L5 up-arrow visible;
- L6 down-arrow visible;
- L4 accept icon visible.

Route editing:
- Route Manager icon hidden;
- speed-edit icons are shown only when a waypoint is selected;
- delete-waypoint icon (L4) is shown only when `RouteEditor.CanDeleteSelectedWaypoint` is true — i.e. a waypoint is selected **and** the route has more than the minimum waypoint count. The icon disappearing (rather than remaining visible-but-ineffective) at the minimum count is the user-facing feedback that deletion isn't currently possible; no separate message is shown.

`UpdateContextualIcons()` now also explicitly resets the Route-Manager-context indicators (`CursorUpIndicator`, `CursorDownIndicator`, `AcceptRouteIndicator`, `AssignedRouteIndicator`) to hidden on entry to creation mode, closing a latent gap where they were only ever explicitly hidden in the non-creation branch.

Icons are simple Avalonia presentation elements whose visibility is explicitly controlled by `TacticalMapView`.

### Route editing presentation
- Route creation has inactive/active visual states.
- Entering route editing hides Route Manager.
- Leaving route editing does not automatically reopen Route Manager.
- Selected waypoint receives a small map selection halo.
- Selected waypoint speed is displayed in the speed-edit panel.
- Speed changes update the route layer and refresh the map.
- Waypoint deletion updates the route layer, clears the selection highlight and speed display, and refreshes the map, using the same refresh sequence already used for speed adjustment.

### UI design philosophy
- Keep controls deliberately lightweight.
- Controls may directly manage local presentation state.
- Do not introduce a ViewModel merely to wrap a simple control.
- Use `INotifyPropertyChanged` or Avalonia property infrastructure only where an actual binding requirement justifies it.
- Prefer simple methods/events over command/property frameworks where sufficient.
- Keep UI state separate from authoritative domain state.
- Keep code explicit, readable, and easy to trace.

---

## 5. Constraints

### Architecture constraints
- `TacticalMapViewModel` receives MFD input and routes commands; it does not manage routes.
- `RouteManagerControl` manages route-list UI state; it does not own route domain state.
- `RouteManager` owns routes, `CurrentRoute`, and `AssignedRoute`; it does not know about UI or MFD interaction.
- `TacticalMapView` owns map presentation, `RouteManagerControl`, and `RouteEditor`.
- `RouteEditor` has no knowledge of `RouteManager`. Waypoint-deletion validity against route assignment (if ever required) belongs at the `TacticalMapView` coordination boundary, following the same pattern as the existing `IsAssigned(Route?)` helper — not inside `RouteEditor` itself. (As implemented, waypoint deletion does not depend on assignment state at all — see §1.)
- `RouteLayer` renders; it does not own route state.
- Own Vehicle simulation state remains outside the map view.
- `OwnVehicle` tracks its navigation target by waypoint **reference**, not list index, specifically so it remains correct when `RouteEditor` mutates the shared `Route.Waypoints` list (including deletion) out from under active navigation. See `GBMS_Route_Assignment_Navigation_Design.md` §1.

### Interaction constraints
- Route management and route editing are distinct functions.
- Route Manager and Route Editor are mutually exclusive presentation modes.
- Route Manager display does not make the mouse a list-navigation mechanism.
- Mapsui retains normal mouse/trackball pan and zoom behaviour.
- Waypoint dragging temporarily locks Mapsui panning and restores it afterwards.
- R1–R6 remain fixed map controls.
- L5/L6 are contextual selection/adjustment controls.
- L4 is context-dependent: Route Manager Accept/Select, Normal Tactical Map route assignment, or Route Editing waypoint deletion.
- L3 toggles Route Manager.
- Waypoint pointer hit-testing (selection and drag-start) is screen-space (pixel radius), not ground-space, so it remains equally usable at any zoom level. Waypoint dragging itself was already screen/world-transform based with no distance threshold, and required no change.

### MVP / KISS constraints
- GBMS Tactical Map is an MVP/concept implementation, not production software.
- Avoid speculative abstractions and premature generalisation.
- Reuse existing services, layers, events, and controls.
- Add infrastructure only when a concrete requirement needs it.
- Prefer explicit readable code over indirection.
- Keep presentation state local when it has no domain significance.
- Do not duplicate authoritative domain state in UI controls.
- Do not conflate Route Editor responsibilities with Route Manager responsibilities.

### Map constraints
- Mapsui v5 is the map presentation technology.
- Spherical Mercator is used at the map boundary.
- Ground-distance operations must account for latitude where longitude distance is involved.
- Geographic route/waypoint geometry must remain meaningful under Mercator presentation.
- Pointer/UI hit-testing and highlight sizing use screen space, not ground distance, so they remain usable/consistent across zoom levels; ground-distance calculations remain appropriate for navigation and geometry (e.g. `OwnVehicle`, geodesic circles).
- `MapControl.RefreshGraphics()` is explicitly used after dynamic tactical/route graphics changes.
- Development currently uses online OSM tiles; deployed operation is intended to use local cached map data.
- Mapsui's API has changed materially across recent versions; new API usage should be verified against the installed package version rather than assumed from documentation/examples, which may reflect older APIs.

### Persistence constraints
- Route persistence belongs to `RouteManager` and its persistence mechanism.
- `RouteManagerControl` does not perform persistence.
- `RouteEditor` manages a temporary editing route independently of Route Manager.
- Integration between edited routes and persistent Route Manager routes is added only as the agreed workflow requires.
- `RoutePersistence`'s minimum waypoint count (2) is now also enforced proactively at delete time by `RouteEditor.CanDeleteSelectedWaypoint`, rather than only being discovered as a save-time validation failure.

### Current implementation status
Completed:
- Own Vehicle tactical symbol and simulation-driven map updates.
- Centre, zoom, and pan controls.
- Route creation/editing foundation.
- Waypoint creation from map interaction.
- Waypoint selection and dragging, using screen-space pixel-radius hit-testing (revised from an earlier ground-distance approach that didn't scale correctly across zoom levels).
- Mapsui pan lock during waypoint dragging.
- Waypoint speed editing.
- Waypoint deletion (L4 in Route Editing context), guarded by minimum waypoint count.
- Route Manager route loading/display.
- Lightweight Route Manager cursor navigation via L5/L6.
- Route Manager Accept via L4.
- Route selection through `RouteManager.CurrentRoute`.
- `CurrentRouteChanged` propagation to Tactical Map.
- Selected-route display through `RouteLayer`.
- Route Manager / Route Editor mutual exclusion.
- Contextual L5/L6 and Accept icon presentation.
- Route assignment to Own Vehicle and waypoint-following navigation (see `GBMS_Route_Assignment_Design.md`, `GBMS_Route_Assignment_Navigation_Design.md`).

### Next incremental work
Remaining open items (see `GBMS_Route_Assignment_Navigation_Design.md` §6 for full detail):
- Clearing the assigned route (un-assigning) — not implemented, required.
- Operator-facing UX for route deletion — the data-layer guard already exists (`RouteManager.RemoveRoute`), but there is no UI path to trigger deletion at all.
- Looping, re-routing, or reversing a route.
- Multiple vehicles or multiple simultaneous routes.

Continue from the established workflow one operation at a time, as with all prior increments.
