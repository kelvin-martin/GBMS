# GBMS Tactical Map — Architecture Design

**Status:** MVP architecture baseline, with route assignment/navigation lifecycle implemented and tested.
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
- A single `RouteLayer` instance is sufficient — an earlier prototype added a second, independent layer to render the assigned route separately from whatever was being browsed, but this was rolled back once Route Manager was made unavailable while a route is assigned (see §3, Route Manager availability). With that invariant in place, the displayed route and the assigned route are always the same object whenever an assignment exists, so a second layer is unnecessary. See `GBMS_Route_Assignment_Design.md` §4 for the full history of this decision.

### SimulationClock / SimulationLoop
- `SimulationClock` provides simulation time.
- `SimulationLoop` drives simulation updates independently of UI presentation.
- Tactical-map simulation updates arrive through `TacticalMapViewModel.MapUpdateRequested`.
- `TacticalMapView` responds by updating the Own Vehicle symbol and calling `MapControl.RefreshGraphics()`.
- UI refresh frequency is deliberately decoupled from simulation/rendering frequency.

### RouteManager
- `RouteManager` is the authoritative owner of:
  - route collection;
  - `CurrentRoute` and `AssignedRoute`;
  - route selection, assignment, and un-assignment;
  - route addition/removal;
  - route persistence.
- `CurrentRouteChanged` is the presentation boundary for a newly selected/displayed route.
- `AssignedRouteChanged` is the presentation boundary for a change in Own Vehicle's assignment.
- `RoutesChanged` allows route-list presentation to refresh.
- Route Manager knows nothing about MFD buttons, cursor/scrolling presentation, or map rendering.
- Full detail on assignment (`AssignRoute`, `ClearAssignedRoute`, the `CurrentRoute == AssignedRoute` invariant while assigned) is in `GBMS_Route_Assignment_Design.md`; vehicle-side behaviour is in `GBMS_Route_Assignment_Navigation_Design.md`.

### RouteEditor
- `RouteEditor` is a lightweight service owned by `TacticalMapView`.
- It manages the route currently being created/edited on the Tactical Map.
- It owns temporary/current editing route, creation-mode state, selected waypoint, dragged waypoint, waypoint addition/movement/deletion, and waypoint speed adjustment.
- It has no knowledge of `RouteManager`.
- A temporary route is retained when leaving creation mode; re-entering creation resumes editing that route. While a route is assigned, this route is always the assigned route (`RouteEditor.CurrentRoute` tracks `RouteManager.CurrentRoute`, which per the assignment doc's invariant is the assigned route whenever one exists) — see "Route Manager availability" below for what this means for building new routes.
- Route editing is deliberately separate from route management.

### RouteLayer
- `RouteLayer` renders the route supplied by `TacticalMapView`.
- `TacticalMapView` calls `SetRoute(...)` when the displayed route changes or when an edited route changes.
- Rendering remains separate from route ownership and editing.

### TacticalSymbolLayer / Own Vehicle
- `TacticalSymbolLayer` renders the Own Vehicle tactical symbol.
- `TacticalMapView.UpdateOwnVehicleSymbol()` reads `TacticalMapViewModel.OwnVehicleState` and passes it to the layer.
- Simulation-driven map updates explicitly refresh graphics.
- L1 centres the map on the current Own Vehicle position, and also cancels a pending route deletion if Route Manager is displayed and armed (see "Route deletion" below).
- Own Vehicle is stationary until a route is assigned, holds stationary again once a route is fully navigated, and reports a live, computed `Speed` rather than a fixed constant. Full detail in `GBMS_Route_Assignment_Navigation_Design.md`.

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
- `OwnVehicleState.Speed` is now a live value reflecting actual vehicle speed (zero while stationary, the current leg's speed while navigating) rather than a fixed placeholder constant. See `GBMS_Route_Assignment_Navigation_Design.md` §2. No UI currently consumes this value; it is intended for a future vehicle system page.

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

R1–R6 remain fixed map-view controls and are not contextual. They deliberately do **not** cancel a pending route deletion (see "Route deletion" below) — they're map-inspection tools, not route-management actions, so an operator can zoom or pan to confirm which route is highlighted without losing progress through a delete confirmation.

### Contextual MFD controls

| Button | Normal Tactical Map | Route Manager | Route Editing |
|---|---|---|---|
| L1 | Centre on Own Vehicle | Centre on Own Vehicle (also cancels a pending delete) | Centre on Own Vehicle |
| L2 | Request route creation (also cancels a pending delete) | Existing route-creation context | Existing editor context |
| L3 | Show/hide Route Manager — **unavailable while a route is assigned** | Show/hide Route Manager | Existing editor context |
| L4 | Assign `CurrentRoute`, **or** unassign the current assignment | Select highlighted route (first press), then delete it (second press, armed) | Delete selected waypoint |
| L5 | — | Move route cursor up | Increase selected waypoint speed |
| L6 | — | Move route cursor down | Decrease selected waypoint speed |

The established L5/L6 speed-edit mapping is the current implementation. Full detail on Normal Tactical Map's L3/L4 assignment behaviour is in `GBMS_Route_Assignment_Design.md` §3–§4.

### Route Manager interaction

The route-management flow, including the two-stage delete confirmation:

```text
L3 (only reachable when no route is assigned)
  → TacticalMapView
  → RouteManagerControl.Show(...)
  → route collection visible

L5/L6
  → TacticalMapView
  → RouteManagerControl.MoveSelectionUp/Down()
  → local UI cursor changes

L4 (first press on a newly-highlighted route)
  → TacticalMapView
  → RouteManagerControl.RequestAcceptOrDelete()
  → RouteManagerControl.Select()
  → RouteManager.SelectRoute(id)
  → CurrentRouteChanged
  → TacticalMapView
  → RouteLayer.SetRoute(route, isAssigned)
  → MapControl.RefreshGraphics()

L4 (second press, same route still highlighted - now armed for deletion)
  → TacticalMapView
  → RouteManagerControl.RequestAcceptOrDelete()
  → RouteManager.RemoveRoute(id)
  → RoutesChanged
  → RouteManagerControl refreshes its list
```

Critical distinctions:
- L5/L6 change only the Route Manager **UI cursor**.
- The **first** L4 press on a given route performs the genuine route selection (as before).
- The **second** L4 press, with the same route still highlighted and no intervening cancel, deletes it.
- Cursor movement does not change `RouteManager.CurrentRoute`, and does not arm or disarm a pending delete on its own — moving the cursor to a *different* route implicitly starts over, since the armed state is tied to the specific highlighted route.

### RouteManagerControl

`RouteManagerControl` obtains the application `RouteManager` from `ApplicationFactory.RouteManager`.

It owns:
- route-list presentation;
- local cursor/selection index;
- last-selected route ID;
- displayed/hidden state;
- cursor movement;
- selection forwarding;
- **the route deletion arm/confirm sequence** (see below);
- deletion forwarding.

It does not own:
- authoritative route domain data;
- map rendering;
- route editing;
- MFD input;
- map pointer interaction.

#### Route deletion (two-stage arm/confirm)

Deleting a route uses a deliberate two-press confirmation on L4, exposed through:
- `RequestAcceptOrDelete()` — called on every L4 press while Route Manager is displayed. Selects the highlighted route on the first press; deletes it on a second, consecutive press against the same highlighted route.
- `IsPendingDelete` — true once armed, read by `TacticalMapView.UpdateContextualIcons()` to switch the L4 icon from "accept" to "delete."
- `CancelPendingDelete()` — disarms without deleting. Called explicitly from L1 (centre on vehicle) and L2 (route creation request), so those actions can proceed normally without silently carrying a stale armed state into an unrelated context. R1–R6 deliberately do **not** cancel (see the fixed MFD controls table above).

Confirmed behaviours:
- Pressing L4 twice in a row deletes the highlighted route on the second press.
- L1, L2, moving the map, or changing zoom **do not** reset the armed state if they aren't one of the explicit cancel triggers above — pressing L4, then panning/zooming, then L4 again still deletes on that third press.
- A route that is `CurrentRoute` or `AssignedRoute` never arms for deletion in a way that can succeed — `RemoveRoute`'s domain-layer guard throws if either applies. Given the assignment doc's mutual-exclusion invariant, Route Manager cannot be open at all while a route is assigned, so in practice this guard is now primarily relevant to `CurrentRoute` (a route can still be displayed without being assigned).
- **No timeout.** An armed delete stays armed indefinitely until confirmed or explicitly cancelled by one of the triggers above. This is a deliberate MVP decision, not an oversight — introducing an expiry window adds a clock to manage and an edge case (did it expire mid-keypress) for a low-stakes failure mode (worst case: an accidental delete on a stray third L4 press, which is already one deliberate keypress removed from a fixed-key layout). Revisit only if real usage shows people walking away mid-arm and returning to a surprise delete.

### CurrentRouteChanged → map
`TacticalMapView` independently references the same application `RouteManager` and subscribes to `CurrentRouteChanged` and `AssignedRouteChanged`.

```text
RouteManager
     │
     ├── CurrentRouteChanged ──────┐
     │                              ▼
     │                       TacticalMapView
     │                              │
     │                              ▼
     │                       RouteLayer.SetRoute(route, isAssigned)
     │                              │
     │                              ▼
     │                       MapControl.RefreshGraphics()
     │
     └── AssignedRouteChanged ─────► TacticalMapView
                                     (restyles the displayed route if its
                                     assigned status changed; see
                                     GBMS_Route_Assignment_Design.md)
```

### Route Manager availability during assignment

Route Manager cannot be displayed while `RouteManager.AssignedRoute != null`. L3 is a silent no-op in that state, and its contextual icon is hidden rather than shown-but-inert. The full rationale, the invariant this establishes (`CurrentRoute == AssignedRoute` whenever a route is assigned), and the rolled-back alternative design (a second, always-visible "assigned route" map layer) are documented in `GBMS_Route_Assignment_Design.md` §4.

One consequence for this doc specifically: because `RouteEditor.CurrentRoute` tracks `RouteManager.CurrentRoute`, L2 (route creation) always resumes editing the assigned route while one is active, rather than starting a new route. Building a genuinely new route requires unassigning first (L4). This is expected, not a bug — see the assignment doc §4 for why no shortcut around it was added.

### Route creation exit behaviour — auto-select

When route creation mode is exited (L2, while active) and a route was actually built or edited, `TacticalMapView.OnRouteCreationRequested` persists it via `RouteManager.AddRoute(...)` as before, and then — **new** — automatically selects it:

```csharp
_routeManager.AddRoute(editedRoute);

// Auto-select on exit so the route is immediately assignable via L4,
// without a trip through Route Manager. Skipped while a route is
// already assigned - Route Manager is locked out in that state, so
// selecting something else here would leave the assigned route with
// no path back onto the map until unassigned.
if (_routeManager.AssignedRoute == null)
{
    _routeManager.SelectRoute(editedRoute.Id);
}
```

This closes a usability gap where a newly created route had to be manually found and selected via Route Manager before it could be assigned via L4 — the assign icon and L4's assign behaviour depend on `CurrentRoute` being set, and previously nothing set it until the operator opened Route Manager, moved the cursor to the new route, and pressed L4 to select it.

Deliberately **not** extended to auto-*assign*: assignment remains a separate, explicit L4 press. Auto-assigning would mean finishing a route immediately puts Own Vehicle underway on it and simultaneously re-locks Route Manager (since assigning implies a route is assigned) — preventing exactly the workflow of building several routes in one session before committing any of them. Auto-select alone removes the friction of the extra Route Manager trip without removing the deliberateness of the assign action itself.

The guard (`AssignedRoute == null`) exists because, without it, creating a new route while one is already assigned would make the new route `CurrentRoute` — but Route Manager, the only way to get back to displaying the assigned route, is locked out in that state, leaving no path back onto the map for the assigned route until it's unassigned. Note that building a new route while one is assigned is itself not currently possible anyway (§ above — L2 resumes editing the assigned route in that state), so this guard is presently a defensive measure against a scenario that can't yet be reached through the UI, rather than one that is.

### Route editing pointer interaction
`TacticalMapView` owns Mapsui pointer interaction for route editing.

- Map tap in creation mode converts the Mapsui world position from Spherical Mercator to latitude/longitude.
- `RouteEditor.AddWaypoint(...)` creates the waypoint.
- The route layer is updated and graphics refreshed.
- Existing waypoints can be selected and dragged.
- During waypoint dragging, Mapsui panning is locked.
- On drag completion, normal Mapsui panning is restored and pointer capture is released.
- A selected waypoint can be deleted via L4 while in Route Editing, subject to the route's minimum waypoint count (`RouteEditor.CanDeleteSelectedWaypoint`).
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
- L4 selects the highlighted route on first press, deletes it on a confirming second press.
- Displaying Route Manager does not enable route editing.
- Route Manager and Route Editor are mutually exclusive.
- Route Manager and an active route assignment are also mutually exclusive (see §3, Route Manager availability during assignment) — this is new since the original baseline and is the most significant interaction change in this revision.

### Route Manager cursor
- `_selectedIndex` is local UI cursor state.
- `_lastSelectedRouteId` restores the cursor when the control is reopened.
- Opening the control restores the last selected route where possible; otherwise the first route is selected.
- Cursor movement is bounded at the first/last item.
- `SelectionIndicator` provides the visible `>` marker.
- Only the dynamic indicator requires property notification.

### Contextual icons
`UpdateContextualIcons()` derives visibility from existing state rather than introducing another state machine. As of this revision it must be called after **any** action that changes `AssignedRoute`, `CurrentRoute`, or Route Manager's displayed/pending-delete state — icon visibility is entirely derived, with no other refresh trigger, and every branch omitting this call has previously been a real, caught bug.

Normal map, Route Manager hidden, nothing assigned:
- centre-vehicle icon visible;
- Route Manager icon visible;
- L5/L6 Route Manager cursor icons hidden;
- assign icon visible only if `CurrentRoute != null`;
- unassign icon hidden.

Normal map, Route Manager hidden, a route assigned:
- centre-vehicle icon visible;
- Route Manager icon **hidden** (L3 is a no-op in this state, so its icon doesn't invite a press);
- unassign icon visible;
- assign icon hidden.

Route Manager displayed (only reachable when nothing is assigned):
- Route Manager icon visible;
- L5 up-arrow visible;
- L6 down-arrow visible;
- accept icon visible while not armed for deletion; delete icon visible instead once armed (`RouteManagerControl.IsPendingDelete`).

Route editing:
- Route Manager icon hidden;
- speed-edit icons shown only when a waypoint is selected;
- delete-waypoint icon shown only when a waypoint is selected and deletion wouldn't take the route below its minimum waypoint count.

Icons are simple Avalonia presentation elements whose visibility is explicitly controlled by `TacticalMapView`.

### Route editing presentation
- Route creation has inactive/active visual states.
- Entering route editing hides Route Manager, and cancels any pending route deletion in Route Manager if one was armed.
- Leaving route editing does not automatically reopen Route Manager, but does auto-select the just-created/edited route if nothing is currently assigned (see §3).
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
- `RouteManagerControl` manages route-list UI state and the delete arm/confirm sequence; it does not own route domain state.
- `RouteManager` owns routes, `CurrentRoute`, and `AssignedRoute`; it does not know about UI or MFD interaction.
- `TacticalMapView` owns map presentation, `RouteManagerControl`, and `RouteEditor`.
- `RouteEditor` has no knowledge of `RouteManager`.
- `RouteLayer` renders; it does not own route state.
- Own Vehicle simulation state remains outside the map view.

### Interaction constraints
- Route management and route editing are distinct functions.
- Route Manager and Route Editor are mutually exclusive presentation modes.
- Route Manager and an active route assignment are mutually exclusive — Route Manager cannot be displayed while `AssignedRoute != null` (new; see `GBMS_Route_Assignment_Design.md` §4).
- Route Manager display does not make the mouse a list-navigation mechanism.
- Mapsui retains normal mouse/trackball pan and zoom behaviour.
- Waypoint dragging temporarily locks Mapsui panning and restores it afterwards.
- R1–R6 remain fixed map controls, and deliberately do not cancel a pending route deletion.
- L5/L6 are contextual selection/adjustment controls.
- L4 is context- and state-dependent: select/delete in Route Manager, assign/unassign in Normal Tactical Map, delete-waypoint in Route Editing.
- L3 toggles Route Manager, except when a route is assigned, in which case it is a no-op.

### MVP / KISS constraints
- GBMS Tactical Map is an MVP/concept implementation, not production software.
- Avoid speculative abstractions and premature generalisation.
- Reuse existing services, layers, events, and controls.
- Add infrastructure only when a concrete requirement needs it.
- Prefer explicit readable code over indirection.
- Keep presentation state local when it has no domain significance.
- Do not duplicate authoritative domain state in UI controls.
- Do not conflate Route Editor responsibilities with Route Manager responsibilities.
- Prefer resolving state-divergence problems by making the divergence structurally unreachable (e.g. Route Manager availability, §3) over compensating for it in rendering (e.g. the rolled-back second route layer) — the former was demonstrated to need less code and introduce fewer edge cases during this project.

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
- Waypoint selection, dragging, and deletion.
- Mapsui pan lock during waypoint dragging.
- Waypoint speed editing.
- Route Manager route loading/display.
- Lightweight Route Manager cursor navigation via L5/L6.
- Route Manager Accept via L4 (first press).
- Route Manager delete via L4 (armed second press), with L1/L2 cancelling and R1–R6 deliberately not cancelling, and no arm timeout.
- Route selection through `RouteManager.CurrentRoute`.
- `CurrentRouteChanged` propagation to Tactical Map.
- Selected-route display through `RouteLayer`.
- Route Manager / Route Editor mutual exclusion.
- Contextual L5/L6 and Accept/Delete icon presentation.
- Route assignment to Own Vehicle via L4 (`RouteManager.AssignRoute`).
- Route un-assignment via L4 (`RouteManager.ClearAssignedRoute`).
- Route Manager / active-assignment mutual exclusion (L3 gating).
- Own Vehicle route-following navigation, turn-rate-limited heading changes.
- Own Vehicle stationary-when-unassigned behaviour (replacing earlier placeholder straight-bearing motion).
- Live, computed `OwnVehicle.Speed`.
- Auto-selection of newly created/edited routes on exiting Route Editor.

### Next incremental work
The core route lifecycle (create → select → assign → navigate → unassign, plus delete) is now complete and tested end-to-end. Remaining known gaps, not yet designed:
- A vehicle system page to surface `OwnVehicleState.Speed` and other live vehicle state.
- Looping, re-routing, or reversing a route.
- Multiple vehicles or multiple simultaneous routes.

Any further Route Manager or Route Editor behaviour should continue to be designed and tested independently, one operation at a time, following the pattern established so far.
