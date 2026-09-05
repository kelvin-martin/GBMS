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
- Future responsibility includes assignment of a route to Own Vehicle.

### RouteEditor
- `RouteEditor` is a lightweight service owned by `TacticalMapView`.
- It manages the route currently being created/edited on the Tactical Map.
- It owns temporary/current editing route, creation-mode state, selected waypoint, dragged waypoint, waypoint addition/movement, and waypoint speed adjustment.
- It has no knowledge of `RouteManager`.
- A temporary route is retained when leaving creation mode; re-entering creation resumes editing that route.
- Route editing is deliberately separate from route management.

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
| L4 | — | Accept/select highlighted route | Existing editor context |
| L5 | — | Move route cursor up | Increase selected waypoint speed |
| L6 | — | Move route cursor down | Decrease selected waypoint speed |

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
- speed-edit icons are shown only when a waypoint is selected.

Icons are simple Avalonia presentation elements whose visibility is explicitly controlled by `TacticalMapView`.

### Route editing presentation
- Route creation has inactive/active visual states.
- Entering route editing hides Route Manager.
- Leaving route editing does not automatically reopen Route Manager.
- Selected waypoint receives a small map selection halo.
- Selected waypoint speed is displayed in the speed-edit panel.
- Speed changes update the route layer and refresh the map.

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
- `RouteManager` owns routes and `CurrentRoute`; it does not know about UI or MFD interaction.
- `TacticalMapView` owns map presentation, `RouteManagerControl`, and `RouteEditor`.
- `RouteEditor` has no knowledge of `RouteManager`.
- `RouteLayer` renders; it does not own route state.
- Own Vehicle simulation state remains outside the map view.

### Interaction constraints
- Route management and route editing are distinct functions.
- Route Manager and Route Editor are mutually exclusive presentation modes.
- Route Manager display does not make the mouse a list-navigation mechanism.
- Mapsui retains normal mouse/trackball pan and zoom behaviour.
- Waypoint dragging temporarily locks Mapsui panning and restores it afterwards.
- R1–R6 remain fixed map controls.
- L5/L6 are contextual selection/adjustment controls.
- L4 is the Route Manager Accept/Select button.
- L3 toggles Route Manager.

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
- `MapControl.RefreshGraphics()` is explicitly used after dynamic tactical/route graphics changes.
- Development currently uses online OSM tiles; deployed operation is intended to use local cached map data.

### Persistence constraints
- Route persistence belongs to `RouteManager` and its persistence mechanism.
- `RouteManagerControl` does not perform persistence.
- `RouteEditor` manages a temporary editing route independently of Route Manager.
- Integration between edited routes and persistent Route Manager routes is added only as the agreed workflow requires.

### Current implementation status
Completed:
- Own Vehicle tactical symbol and simulation-driven map updates.
- Centre, zoom, and pan controls.
- Route creation/editing foundation.
- Waypoint creation from map interaction.
- Waypoint selection and dragging.
- Mapsui pan lock during waypoint dragging.
- Waypoint speed editing.
- Route Manager route loading/display.
- Lightweight Route Manager cursor navigation via L5/L6.
- Route Manager Accept via L4.
- Route selection through `RouteManager.CurrentRoute`.
- `CurrentRouteChanged` propagation to Tactical Map.
- Selected-route display through `RouteLayer`.
- Route Manager / Route Editor mutual exclusion.
- Contextual L5/L6 and Accept icon presentation.

### Next incremental work
Continue from the established Route Manager workflow one operation at a time. The next Route Manager behaviour should be designed and tested independently, with particular attention to the effect of deletion on `CurrentRoute`, cursor position, and map presentation.
