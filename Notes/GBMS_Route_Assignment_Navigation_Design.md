# GBMS — Own Vehicle Navigation

**Status:** Implemented and tested.
**Scope:** Replaced Own Vehicle's placeholder straight-bearing movement with waypoint-following navigation, driven by `RouteManager.AssignedRoute`, including turn-rate-limited heading changes.

**Depends on:** `GBMS_Tactical_Map_Architecture_Design.md`, `GBMS_Route_Assignment_Design.md`

**History:** an early draft of this doc proposed a standalone `OwnVehicleNavigator` component with its own `AssignedRoute`. That was dropped once it became clear assignment already lived on `RouteManager` — the final design extends `OwnVehicle` directly instead.

---

## 1. Final design

`OwnVehicle` (`GBMS.Models`) gained route-following behaviour without any new component:

- `AssignRoute(Route? route)` — sets the assigned route and resets progress to the route's first waypoint. `null` clears the assignment.
- `Update(TimeSpan simulationStep)` has three branches:
  1. No route assigned → **unchanged placeholder motion** (straight bearing, fixed `Speed`).
  2. Route assigned, target waypoint present → `UpdateNavigating`.
  3. Route assigned, no target waypoint (all waypoints reached, or the target was removed from the route) → hold position and heading (early return, no movement). This is a distinct branch from (1) — falling through to placeholder motion here would have driven the vehicle straight past the final waypoint instead of stopping.
- `UpdateNavigating` each tick:
  - Computes great-circle distance and bearing from current position to the current target waypoint.
  - Converts the waypoint's `Speed` from km/h to m/s (`÷ 3.6`) — waypoint speeds are km/h; vehicle motion works in m/s throughout.
  - Applies a **turn-rate limit** (see §2) rather than snapping heading directly to the bearing.
  - If remaining distance ≤ this tick's travel distance: snaps to the waypoint and advances to the next target. Any leftover travel distance this tick is not carried into the next waypoint — acceptable at typical tick rates/speeds for MVP.
  - Otherwise projects a new position along the turn-limited heading for this tick's travel distance.

`OwnVehicle` never takes a dependency on `RouteManager` — it only ever receives a `Route` by direct method call, the same isolation `RouteEditor` already has from `RouteManager`. Whoever owns the `OwnVehicle` instance (`SimulationManager`) is responsible for the `RouteManager.AssignedRouteChanged` → `AssignRoute(...)` forwarding (see §3).

### Target tracking: by reference, not by index (revised)

**Original approach (superseded):** `OwnVehicle` tracked navigation progress with `_currentWaypointIndex`, a plain integer position into `_assignedRoute.Waypoints`.

**Problem found:** `RouteEditor` and `OwnVehicle` can hold a reference to the same `Route` instance simultaneously (the route currently displayed/edited can also be the assigned route — see §6 "Resolved: editing the assigned route while navigating" below). Once waypoint deletion was implemented in `RouteEditor` (see `GBMS_Tactical_Map_Architecture_Design.md`), deleting a waypoint shifts every subsequent waypoint down one position in the list. `_currentWaypointIndex` did not move with it, so it silently came to point at the wrong waypoint:
- Deleting a waypoint **before** the current target caused Own Vehicle to skip its intended target and head to the *next* waypoint after it instead (confirmed by testing: A–B–C–D, heading to C, deleting A caused the vehicle to head to D, skipping C).
- If the skipped-past target happened to be the last waypoint, the shifted index could land exactly on `Waypoints.Count`, tripping the "all waypoints reached" condition early — causing Own Vehicle to stop just short of the true final waypoint, having silently skipped it.

Both symptoms were the same underlying bug: an index is a *position*, not an *identity*, and `RouteEditor`'s mutation changed positions without `OwnVehicle` knowing.

**Fix:** `OwnVehicle` now tracks its target as a direct **reference to the `Waypoint` object** (`_currentTargetWaypoint`), not an index:

- `AssignRoute` sets `_currentTargetWaypoint` to `route.Waypoints[0]` (or `null` for an empty route or `null` route).
- Each `Update` tick checks whether `_currentTargetWaypoint` is still present in `_assignedRoute.Waypoints` (`Contains`). If it isn't — because the route is exhausted, or because that specific waypoint was deleted — Own Vehicle holds position, same as reaching the end of the route.
- On arrival at a waypoint, the next target is found via `IndexOf` on the just-reached waypoint plus one, rather than by incrementing a stored index.

This is correct where index-tracking wasn't, because a reference to a specific `Waypoint` object is unaffected by other elements shifting around it in the list — deleting an earlier, already-passed waypoint no longer disturbs which object `OwnVehicle` is aiming at.

**Behavioural note (intentional change from index-tracking):** deleting the waypoint that is *itself* the current navigation target now causes Own Vehicle to hold position rather than silently retargeting to whatever waypoint slid into that list slot. This is considered the more correct behaviour — the operator explicitly removed the thing being navigated to, so holding and waiting (consistent with the existing "all waypoints reached" behaviour) is more honest than continuing toward an object that happens to occupy the vacated position. This was a behavioural side effect of the previous index-based implementation, not a deliberately designed feature, so its removal is treated as a fix rather than a regression.

**Cost:** `Contains`/`IndexOf` are O(n) in route length, versus O(1) for a raw index. For MVP route sizes at the existing 10 Hz simulation tick rate, this is immaterial and not worth a more complex structure (e.g. a linked waypoint chain).

Both scenarios (deleting an already-passed waypoint; deleting a waypoint near the end of the route) have been tested and confirmed working correctly with this fix.

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

Waypoint deletion (see `GBMS_Tactical_Map_Architecture_Design.md`) requires no additional wiring here either — `OwnVehicle` observes the mutated `Route.Waypoints` list directly on its next tick, via the same shared-reference relationship as route editing (§6).

### Startup ordering issue (found and fixed)
`ApplicationFactory.Initialize()` originally constructed `SimulationManager` **before** `RouteManager`. Since `SimulationManager`'s constructor reads `ApplicationFactory.RouteManager`, this threw `InvalidOperationException` at startup — not a real circular dependency (`RouteManager` has no dependency on `SimulationManager`), just two lines in the wrong order. Fixed by constructing `RouteManager` first.

---

## 4. Geometry consolidation

While implementing this, duplication surfaced against `Mapping` (`GBMS.Services`), which already had a `CalculateDistanceKm` haversine implementation and an inline destination-point projection formula embedded in `CreateGeodesicCircle`. Resolved by:

- Extracting the destination-point formula out of `CreateGeodesicCircle`'s loop into a new public `Mapping.ProjectPosition(latitude, longitude, bearingDegrees, distanceKm)`, which `CreateGeodesicCircle` now calls instead of duplicating inline.
- `OwnVehicle` now calls `Mapping.CalculateDistanceKm` and `Mapping.ProjectPosition` directly (converting km↔m at the call sites, since `OwnVehicle` works in metres and `Mapping` works in kilometres) instead of keeping its own private copies of that math.
- `OwnVehicle.BearingDegrees` (initial bearing between two points) stayed local/private — nothing else in the codebase currently needs it, so it wasn't promoted to `Mapping` speculatively.

This does give `OwnVehicle` (`GBMS.Models`) a dependency on `Mapping` (`GBMS.Services`) — accepted deliberately, since `Mapping` is a stateless, pure-math utility class, a different kind of coupling than the domain-service dependency (on `RouteManager`) that was avoided elsewhere in this design.

Note: `Mapping.CalculateDistanceKm` is ground-space and remains correct for this navigation use. It is explicitly **not** used for map-pointer hit-testing (waypoint selection/drag-start), which was found to need screen-space distance instead — see `GBMS_Tactical_Map_Architecture_Design.md`, "Waypoint hit-testing (revised — screen-space)".

---

## 5. Constraints carried

- `OwnVehicle` still has no dependency on `RouteManager` or any stateful domain service — only on the pure-math `Mapping` utility (see §4).
- `RouteManager` was unchanged by this increment — assignment work already exposed everything navigation needed.
- No new component introduced (no `OwnVehicleNavigator`) — this extended the existing `OwnVehicle.Update`, consistent with MVP/KISS.
- No acceleration realism — waypoint speed is applied directly, no ramp-up/down (turn rate is the only rate-limited quantity).
- No looping, re-routing, or multi-vehicle support.
- `OwnVehicle` tracks its navigation target by waypoint object reference, not list index, so that it remains correct if the shared `Route.Waypoints` list is mutated (e.g. waypoint deletion) by `RouteEditor` while that route is assigned and being actively navigated.

---

## 6. Remaining open work (not part of this increment)

- **Route deletion.** There is currently no UI path for a user to delete a route at all. Separately, `RouteManager.RemoveRoute` throws if the route being removed is the currently assigned (or currently displayed) one — so the underlying data layer already prevents deleting a route out from under active navigation, but the operator-facing UX for a blocked or successful deletion hasn't been designed. Deferred, flagged as the next open item.
- **Clearing the assigned route** (un-assigning) is not implemented but required — not yet designed. Noted as a possible future need back in the assignment design doc; now confirmed as an actual requirement.
- Looping, re-routing, or reversing a route.
- Multiple vehicles or multiple simultaneous routes.

### Resolved: waypoint deletion during active navigation
Waypoint deletion is now implemented in `RouteEditor` (see `GBMS_Tactical_Map_Architecture_Design.md`). Because `RouteEditor` and `OwnVehicle` can share a reference to the same `Route` (per §6 below), deleting a waypoint from an actively-navigated, assigned route is a real scenario, not a hypothetical one. It is explicitly supported:
- Deleting an already-passed (non-target) waypoint does not disturb navigation — `OwnVehicle`'s reference-based target tracking (§1) is unaffected by other list elements shifting.
- Deleting the current target waypoint causes Own Vehicle to hold position and wait, rather than erroring or silently retargeting — see §1's behavioural note.
- No guard against deleting a waypoint on the assigned route was introduced; this was deliberately decided against once reference-based tracking made it unnecessary; see §1 for the fix that made this safe.

### Resolved: editing the assigned route while navigating (confirmed via testing)
An earlier version of this doc listed this as "not currently reachable, since Route Editor only appears to support creating new routes, not re-editing persisted ones" — that assumption was wrong, confirmed by testing. `TacticalMapView.OnCurrentRouteChanged` calls `_routeEditor.SetRoute(route)` with the *same `Route` object reference* as `RouteManager.CurrentRoute` (and therefore the same reference `OwnVehicle` holds as its assigned route, if that route is assigned). So editing an existing route through Route Editor mutates that shared instance directly — which is also *why* navigation updates live as the route is edited, with no extra wiring needed for that to work.

This did surface a real bug: the four `RouteLayer.SetRoute(_routeEditor.CurrentRoute, false)` call sites (waypoint add, waypoint drag, both speed-adjust keys) hardcoded `isAssigned` to `false`, on the wrong assumption that an in-progress edit could never be the assigned route. Editing the assigned route therefore visibly turned it red, losing the blue "assigned" indicator, for the duration of the edit. Fixed by introducing a single `IsAssigned(Route?)` helper in `TacticalMapView`, used at all six `SetRoute` call sites (the four editing ones, plus `OnCurrentRouteChanged` and `OnAssignedRouteChanged`) — one authoritative comparison instead of six duplicated ones, which is what let one of them silently drift wrong in the first place.

This same shared-reference relationship is what made the waypoint-deletion index bug (§1) possible, and is worth keeping in mind for any future feature that mutates a `Route` — any such mutation is implicitly visible to whichever other component holds a reference to the same route (`OwnVehicle`, `RouteLayer` via `TacticalMapView`), whether or not that was the intent.

---

## 7. Implementation history

1. ~~Share the orchestrating class~~ — `SimulationManager.cs` and `SimulationLoop.cs` provided; confirmed `SimulationManager` as the correct subscription point.
2. ~~Add `AssignRoute`~~ — added to `OwnVehicle`, with placeholder-motion fallback confirmed unaffected.
3. ~~Add waypoint-following logic~~ — implemented in `UpdateNavigating`, including km/h→m/s conversion.
4. ~~Subscribe to `AssignedRouteChanged`~~ — wired in `SimulationManager`'s constructor.
5. ~~Fix startup ordering~~ — `ApplicationFactory.Initialize()` reordered.
6. ~~Consolidate geometry duplication~~ — `Mapping.ProjectPosition` extracted and reused.
7. ~~Add turn-rate limiting~~ — `MaxTurnRateDegreesPerSecond` + `ApplyTurnRateLimit`, tested against large and small angle changes.
8. ~~Fix waypoint hit-testing for map-pointer selection~~ — see `GBMS_Tactical_Map_Architecture_Design.md`; not an `OwnVehicle`/navigation change, but resolved in the same working session.
9. ~~Add waypoint deletion~~ — implemented in `RouteEditor`; see `GBMS_Tactical_Map_Architecture_Design.md`.
10. ~~Fix `OwnVehicle` target tracking for waypoint deletion~~ — switched from index-based to reference-based target tracking (§1). Tested against both "delete an already-passed waypoint" and "delete a waypoint near the end of the route" scenarios; both now behave correctly.

All steps tested end-to-end: assigning a route via L4 turns the vehicle toward the first waypoint, follows the route at correct converted speeds with realistic turning, holds at the final waypoint, and remains correct when waypoints are added, edited, or deleted on the assigned route during active navigation.
