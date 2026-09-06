# GBMS — Own Vehicle Navigation

**Status:** Implemented and tested.
**Scope:** Replaced Own Vehicle's placeholder straight-bearing movement with waypoint-following navigation, driven by `RouteManager.AssignedRoute`, including turn-rate-limited heading changes.

**Depends on:** `GBMS_Tactical_Map_Architecture_Design.md`, `GBMS_Route_Assignment_Design.md`

**History:** an early draft of this doc proposed a standalone `OwnVehicleNavigator` component with its own `AssignedRoute`. That was dropped once it became clear assignment already lived on `RouteManager` — the final design extends `OwnVehicle` directly instead.

---

## 1. Final design

`OwnVehicle` (`GBMS.Models`) gained route-following behaviour without any new component:

- `AssignRoute(Route? route)` — sets the assigned route and resets progress to waypoint `0`. `null` clears the assignment.
- `Update(TimeSpan simulationStep)` has three branches:
  1. No route assigned → **unchanged placeholder motion** (straight bearing, fixed `Speed`).
  2. Route assigned, waypoints remaining → `UpdateNavigating`.
  3. Route assigned, all waypoints reached → hold position and heading (early return, no movement). This is a distinct branch from (1) — falling through to placeholder motion here would have driven the vehicle straight past the final waypoint instead of stopping.
- `UpdateNavigating` each tick:
  - Computes great-circle distance and bearing from current position to the current target waypoint.
  - Converts the waypoint's `Speed` from km/h to m/s (`÷ 3.6`) — waypoint speeds are km/h; vehicle motion works in m/s throughout.
  - Applies a **turn-rate limit** (see §2) rather than snapping heading directly to the bearing.
  - If remaining distance ≤ this tick's travel distance: snaps to the waypoint and advances the index. Any leftover travel distance this tick is not carried into the next waypoint — acceptable at typical tick rates/speeds for MVP.
  - Otherwise projects a new position along the turn-limited heading for this tick's travel distance.

`OwnVehicle` never takes a dependency on `RouteManager` — it only ever receives a `Route` by direct method call, the same isolation `RouteEditor` already has from `RouteManager`. Whoever owns the `OwnVehicle` instance (`SimulationManager`) is responsible for the `RouteManager.AssignedRouteChanged` → `AssignRoute(...)` forwarding (see §3).

---

## 2. Turn-rate limiting

Initial testing showed the vehicle's heading snapping instantly to face each new waypoint — visually unrealistic. Fixed by limiting how much heading can change per tick:

- `OwnVehicle.MaxTurnRateDegreesPerSecond` — public, settable property, default `15.0` (midpoint of the discussed 10–20°/s range). Deliberately just a property rather than a configuration system, per MVP/KISS — it's tunable from outside `OwnVehicle` without any new infrastructure.
- `ApplyTurnRateLimit(currentHeading, desiredHeading, maxTurnThisTick)` computes the signed angular difference (normalized to take the shorter way around the compass — e.g. 350°→10° turns +20°, not −340°), clamps it to `±maxTurnThisTick`, and returns the new heading.
- The turn-limited heading is used consistently for **both** in-transit movement and the arrival/snap-to-waypoint case — so heading keeps turning gradually onto the next leg even at the instant of arrival, rather than snapping to face the just-reached waypoint exactly.
- Tested against both large and small heading changes — confirmed working correctly.

---

## 3. Wiring (as implemented)

```text
RouteManager.AssignedRouteChanged
  → SimulationManager.OnAssignedRouteChanged(route)
  → OwnVehicle.AssignRoute(route)

Every simulation tick (SimulationLoop → SimulationManager.UpdateSimulation, unchanged trigger point):
  OwnVehicle.Update(simulationStep)
  → navigates or holds or runs placeholder motion, per §1

  (existing, unchanged)
  → SimulationState.UpdateOwnVehicle(OwnVehicle)
  → OwnVehicleState snapshot
  → TacticalMapViewModel.OwnVehicleState
  → TacticalMapView.UpdateOwnVehicleSymbol()
  → MapControl.RefreshGraphics()
```

`SimulationManager`'s constructor subscribes to `RouteManager.AssignedRouteChanged`, obtaining `RouteManager` via `ApplicationFactory.RouteManager` — the same access pattern already used elsewhere (e.g. `TacticalMapView`). `UpdateSimulation` itself needed no changes; all navigation logic is internal to `OwnVehicle.Update`.

### Startup ordering issue (found and fixed)
`ApplicationFactory.Initialize()` originally constructed `SimulationManager` **before** `RouteManager`. Since `SimulationManager`'s constructor reads `ApplicationFactory.RouteManager`, this threw `InvalidOperationException` at startup — not a real circular dependency (`RouteManager` has no dependency on `SimulationManager`), just two lines in the wrong order. Fixed by constructing `RouteManager` first.

---

## 4. Geometry consolidation

While implementing this, duplication surfaced against `Mapping` (`GBMS.Services`), which already had a `CalculateDistanceKm` haversine implementation and an inline destination-point projection formula embedded in `CreateGeodesicCircle`. Resolved by:

- Extracting the destination-point formula out of `CreateGeodesicCircle`'s loop into a new public `Mapping.ProjectPosition(latitude, longitude, bearingDegrees, distanceKm)`, which `CreateGeodesicCircle` now calls instead of duplicating inline.
- `OwnVehicle` now calls `Mapping.CalculateDistanceKm` and `Mapping.ProjectPosition` directly (converting km↔m at the call sites, since `OwnVehicle` works in metres and `Mapping` works in kilometres) instead of keeping its own private copies of that math.
- `OwnVehicle.BearingDegrees` (initial bearing between two points) stayed local/private — nothing else in the codebase currently needs it, so it wasn't promoted to `Mapping` speculatively.

This does give `OwnVehicle` (`GBMS.Models`) a dependency on `Mapping` (`GBMS.Services`) — accepted deliberately, since `Mapping` is a stateless, pure-math utility class, a different kind of coupling than the domain-service dependency (on `RouteManager`) that was avoided elsewhere in this design.

---

## 5. Constraints carried

- `OwnVehicle` still has no dependency on `RouteManager` or any stateful domain service — only on the pure-math `Mapping` utility (see §4).
- `RouteManager` was unchanged by this increment — assignment work already exposed everything navigation needed.
- No new component introduced (no `OwnVehicleNavigator`) — this extended the existing `OwnVehicle.Update`, consistent with MVP/KISS.
- No acceleration realism — waypoint speed is applied directly, no ramp-up/down (turn rate is the only rate-limited quantity).
- No looping, re-routing, or multi-vehicle support.

---

## 6. Remaining open work (not part of this increment)

- **Route deletion.** There is currently no UI path for a user to delete a route at all. Separately, `RouteManager.RemoveRoute` throws if the route being removed is the currently assigned (or currently displayed) one — so the underlying data layer already prevents deleting a route out from under active navigation, but the operator-facing UX for a blocked or successful deletion hasn't been designed. Deferred, flagged as the next open item.
- **Waypoint deletion** is not implemented but required — not yet designed.
- **Clearing the assigned route** (un-assigning) is not implemented but required — not yet designed. Noted as a possible future need back in the assignment design doc; now confirmed as an actual requirement.
- Looping, re-routing, or reversing a route.
- Multiple vehicles or multiple simultaneous routes.

### Resolved: editing the assigned route while navigating (confirmed via testing)
An earlier version of this doc listed this as "not currently reachable, since Route Editor only appears to support creating new routes, not re-editing persisted ones" — that assumption was wrong, confirmed by testing. `TacticalMapView.OnCurrentRouteChanged` calls `_routeEditor.SetRoute(route)` with the *same `Route` object reference* as `RouteManager.CurrentRoute` (and therefore the same reference `OwnVehicle` holds as its assigned route, if that route is assigned). So editing an existing route through Route Editor mutates that shared instance directly — which is also *why* navigation updates live as the route is edited, with no extra wiring needed for that to work.

This did surface a real bug: the four `RouteLayer.SetRoute(_routeEditor.CurrentRoute, false)` call sites (waypoint add, waypoint drag, both speed-adjust keys) hardcoded `isAssigned` to `false`, on the wrong assumption that an in-progress edit could never be the assigned route. Editing the assigned route therefore visibly turned it red, losing the blue "assigned" indicator, for the duration of the edit. Fixed by introducing a single `IsAssigned(Route?)` helper in `TacticalMapView`, used at all six `SetRoute` call sites (the four editing ones, plus `OnCurrentRouteChanged` and `OnAssignedRouteChanged`) — one authoritative comparison instead of six duplicated ones, which is what let one of them silently drift wrong in the first place.

---

## 7. Implementation history

1. ~~Share the orchestrating class~~ — `SimulationManager.cs` and `SimulationLoop.cs` provided; confirmed `SimulationManager` as the correct subscription point.
2. ~~Add `AssignRoute`~~ — added to `OwnVehicle`, with placeholder-motion fallback confirmed unaffected.
3. ~~Add waypoint-following logic~~ — implemented in `UpdateNavigating`, including km/h→m/s conversion.
4. ~~Subscribe to `AssignedRouteChanged`~~ — wired in `SimulationManager`'s constructor.
5. ~~Fix startup ordering~~ — `ApplicationFactory.Initialize()` reordered.
6. ~~Consolidate geometry duplication~~ — `Mapping.ProjectPosition` extracted and reused.
7. ~~Add turn-rate limiting~~ — `MaxTurnRateDegreesPerSecond` + `ApplyTurnRateLimit`, tested against large and small angle changes.

All steps tested end-to-end: assigning a route via L4 turns the vehicle toward the first waypoint, follows the route at correct converted speeds with realistic turning, and holds at the final waypoint.
