# GBMS — Own Vehicle Navigation

**Status:** Implemented and tested.
**Scope:** Own Vehicle's motion behaviour when a route is and isn't assigned, driven by `RouteManager.AssignedRoute`, including turn-rate-limited heading changes and a live, computed vehicle speed.

**Depends on:** `GBMS_Tactical_Map_Architecture_Design.md`, `GBMS_Route_Assignment_Design.md`

**History:** an early draft of this doc proposed a standalone `OwnVehicleNavigator` component with its own `AssignedRoute`. That was dropped once it became clear assignment already lived on `RouteManager` — the final design extends `OwnVehicle` directly instead.

---

## 1. Final design

`OwnVehicle` (`GBMS.Models`) has route-following behaviour with no separate component:

- `AssignRoute(Route? route)` — sets the assigned route and resets progress to waypoint `0`. `null` clears the assignment.
- `Update(TimeSpan simulationStep)` has two effective branches:
  1. No route assigned, **or** a route assigned with all waypoints reached → **hold**: position and heading unchanged, `Speed` set to `0`. These were originally two separate cases with different rationale (see "Removed: placeholder motion" below); they now produce identical, deliberately identical, behaviour.
  2. Route assigned, waypoints remaining → `UpdateNavigating`.
- `UpdateNavigating` each tick:
  - Computes great-circle distance and bearing from current position to the current target waypoint.
  - Converts the waypoint's `Speed` from km/h to m/s (`÷ 3.6`) and assigns it directly to `OwnVehicle.Speed` (see §2) — waypoint speeds are km/h; vehicle motion works in m/s throughout.
  - Applies a turn-rate limit (§3) rather than snapping heading directly to the bearing.
  - If remaining distance ≤ this tick's travel distance: snaps to the waypoint and advances the index. Any leftover travel distance this tick is not carried into the next waypoint — acceptable at typical tick rates/speeds for MVP.
  - Otherwise projects a new position along the turn-limited heading for this tick's travel distance.

`OwnVehicle` never takes a dependency on `RouteManager` — it only ever receives a `Route` by direct method call. Whoever owns the `OwnVehicle` instance (`SimulationManager`) is responsible for the `RouteManager.AssignedRouteChanged` → `AssignRoute(...)` forwarding (§4).

### Removed: placeholder straight-bearing motion

The original design gave Own Vehicle a third `Update` branch — a placeholder straight-line motion at a fixed `Speed`, used whenever no route was assigned, standing in for "the vehicle exists and moves somehow" before real navigation existed. **This has been removed.** With route assignment/unassignment now a normal, frequently-used part of the operator workflow (see the assignment doc §3/§4), an unassigned vehicle sliding forever in a straight line is no longer a reasonable idle behaviour. Own Vehicle is now correctly modelled as **stationary** when unassigned — no assigned route means it goes nowhere, using the same "hold" behaviour already in place for reaching the end of a route.

This also directly resolves a rough edge in the assignment design that would otherwise have appeared: pressing unassign mid-navigation used to hand control back to the old placeholder motion, meaning the vehicle would immediately continue moving in whatever direction it last faced. It now correctly comes to an immediate stop.

`UpdatePlaceholder` was deleted; nothing else referenced it.

---

## 2. Vehicle speed — now a live, computed value

`OwnVehicle.Speed` was originally a fixed constant (`30` m/s), documented as being for the placeholder motion but also the value reported through `SimulationState`/`OwnVehicleState` regardless of what the vehicle was actually doing. With the placeholder removed, this was tightened to reflect reality:

```csharp
/// <summary>
/// Current vehicle speed, in metres/second. Zero while stationary (no
/// route assigned, or the assigned route has been fully navigated).
/// While navigating, set each tick to the current target waypoint's
/// speed, converted from km/h.
/// </summary>
public double Speed { get; private set; } = 0.0;
```

- Set to `0.0` in both branches of the "hold" case.
- Set to the converted target-waypoint speed inside `UpdateNavigating`, at the point it's computed, before being used for that tick's travel-distance calculation.

`SimulationState.UpdateOwnVehicle` needed no change — it already read `vehicle.Speed`; that value is now trustworthy rather than a constant placeholder. No consumer of `OwnVehicleState.Speed` exists yet in the UI (the future vehicle system page is the intended consumer) — this change makes the underlying data correct ahead of that page being built, rather than needing revisiting then.

One deliberate consequence, consistent with the existing "no acceleration realism" constraint (§6): a speed change at a waypoint is instantaneous — `Speed` jumps to the new leg's value the moment the previous waypoint is reached, with no ramp.

---

## 3. Turn-rate limiting

Initial testing showed the vehicle's heading snapping instantly to face each new waypoint — visually unrealistic. Fixed by limiting how much heading can change per tick:

- `OwnVehicle.MaxTurnRateDegreesPerSecond` — public, settable property, default `15.0` (midpoint of the discussed 10–20°/s range). Deliberately just a property rather than a configuration system, per MVP/KISS — it's tunable from outside `OwnVehicle` without any new infrastructure.
- `ApplyTurnRateLimit(currentHeading, desiredHeading, maxTurnThisTick)` computes the signed angular difference (normalized to take the shorter way around the compass — e.g. 350°→10° turns +20°, not −340°), clamps it to `±maxTurnThisTick`, and returns the new heading.
- The turn-limited heading is used consistently for **both** in-transit movement and the arrival/snap-to-waypoint case — so heading keeps turning gradually onto the next leg even at the instant of arrival, rather than snapping to face the just-reached waypoint exactly.
- Tested against both large and small heading changes — confirmed working correctly.

---

## 4. Wiring (as implemented)

```text
RouteManager.AssignedRouteChanged
  → SimulationManager.OnAssignedRouteChanged(route)
  → OwnVehicle.AssignRoute(route)

Every simulation tick (SimulationLoop → SimulationManager.UpdateSimulation, unchanged trigger point):
  OwnVehicle.Update(simulationStep)
  → navigates or holds stationary, per §1

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

## 5. Geometry consolidation

While implementing route-following, duplication surfaced against `Mapping` (`GBMS.Services`), which already had a `CalculateDistanceKm` haversine implementation and an inline destination-point projection formula embedded in `CreateGeodesicCircle`. Resolved by:

- Extracting the destination-point formula out of `CreateGeodesicCircle`'s loop into a new public `Mapping.ProjectPosition(latitude, longitude, bearingDegrees, distanceKm)`, which `CreateGeodesicCircle` now calls instead of duplicating inline.
- `OwnVehicle` now calls `Mapping.CalculateDistanceKm` and `Mapping.ProjectPosition` directly (converting km↔m at the call sites, since `OwnVehicle` works in metres and `Mapping` works in kilometres) instead of keeping its own private copies of that math.
- `OwnVehicle.BearingDegrees` (initial bearing between two points) stayed local/private — nothing else in the codebase currently needs it, so it wasn't promoted to `Mapping` speculatively.

This gives `OwnVehicle` (`GBMS.Models`) a dependency on `Mapping` (`GBMS.Services`) — accepted deliberately, since `Mapping` is a stateless, pure-math utility class, a different kind of coupling than the domain-service dependency (on `RouteManager`) that was avoided elsewhere in this design.

---

## 6. Constraints carried

- `OwnVehicle` still has no dependency on `RouteManager` or any stateful domain service — only on the pure-math `Mapping` utility (see §5).
- No new component introduced (no `OwnVehicleNavigator`) — this extended the existing `OwnVehicle.Update`, consistent with MVP/KISS.
- No acceleration realism — waypoint speed is applied directly, no ramp-up/down (turn rate is the only rate-limited quantity).
- No looping, re-routing, or multi-vehicle support.
- **Removed:** the old placeholder straight-bearing motion (see §1) — there is no longer an "idle drift" state; unassigned means stationary.

---

## 7. Remaining open work (not part of this increment)

- **Route deletion** — now has a UI path (a two-stage arm/confirm sequence via L4 within Route Manager). See the Tactical Map Architecture doc's Route Manager section for the mechanism. `RouteManager.RemoveRoute` still throws if the target is `CurrentRoute` or `AssignedRoute`; given the assignment doc's mutual-exclusion invariant, a route can no longer be deleted while it's assigned, since Route Manager can't be displayed at the same time an assignment is active. The domain-layer guard remains in place as a defensive check regardless.
- **Waypoint deletion** — implemented (`RouteEditor.DeleteSelectedWaypoint`, wired to L4 while a waypoint is selected in Route Editing), bounded by the same two-waypoint persistence minimum used elsewhere.
- ~~**Clearing the assigned route (un-assigning)**~~ — implemented; see the assignment design doc, §2 (`ClearAssignedRoute`) and §6.
- Looping, re-routing, or reversing a route.
- Multiple vehicles or multiple simultaneous routes.
- Vehicle system page (speed, other live vehicle state) — not yet built. `OwnVehicle.Speed` is now real, computed data (§2), ready to be consumed once that page exists.

### Resolved: editing the assigned route while navigating (confirmed via testing)
An earlier version of this doc listed this as "not currently reachable, since Route Editor only appears to support creating new routes, not re-editing persisted ones" — that assumption was wrong, confirmed by testing. `TacticalMapView.OnCurrentRouteChanged` calls `_routeEditor.SetRoute(route)` with the *same `Route` object reference* as `RouteManager.CurrentRoute` (and therefore the same reference `OwnVehicle` holds as its assigned route, if that route is assigned). So editing an existing route through Route Editor mutates that shared instance directly — which is also *why* navigation updates live as the route is edited, with no extra wiring needed for that to work.

This did surface a real bug: the four `RouteLayer.SetRoute(_routeEditor.CurrentRoute, false)` call sites (waypoint add, waypoint drag, both speed-adjust keys) hardcoded `isAssigned` to `false`, on the wrong assumption that an in-progress edit could never be the assigned route. Editing the assigned route therefore visibly turned it red, losing the blue "assigned" indicator, for the duration of the edit. Fixed by introducing a single `IsAssigned(Route?)` helper in `TacticalMapView`, used at all six `SetRoute` call sites — one authoritative comparison instead of six duplicated ones, which is what let one of them silently drift wrong in the first place.

This same shared-reference mechanism is also the reason L2 always resumes editing the assigned route rather than starting fresh while a route is assigned — see the assignment design doc §4 for the reasoning and why that's treated as expected behaviour rather than a gap.

---

## 8. Implementation history

1. ~~Share the orchestrating class~~ — `SimulationManager.cs` and `SimulationLoop.cs` confirmed as the correct subscription point.
2. ~~Add `AssignRoute`~~ — added to `OwnVehicle`, with placeholder-motion fallback (since removed, see step 8) confirmed unaffected at the time.
3. ~~Add waypoint-following logic~~ — implemented in `UpdateNavigating`, including km/h→m/s conversion.
4. ~~Subscribe to `AssignedRouteChanged`~~ — wired in `SimulationManager`'s constructor.
5. ~~Fix startup ordering~~ — `ApplicationFactory.Initialize()` reordered.
6. ~~Consolidate geometry duplication~~ — `Mapping.ProjectPosition` extracted and reused.
7. ~~Add turn-rate limiting~~ — `MaxTurnRateDegreesPerSecond` + `ApplyTurnRateLimit`, tested against large and small angle changes.
8. ~~Remove placeholder straight-bearing motion; hold stationary when unassigned~~ — done, `UpdatePlaceholder` deleted, replaced by the existing "hold" branch.
9. ~~Make `Speed` a real computed value instead of a fixed constant~~ — done; `0` while holding, the converted current-waypoint speed while navigating.

All steps tested end-to-end: assigning a route via L4 turns the vehicle toward the first waypoint, follows the route at correct converted speeds with realistic turning, and holds stationary at the final waypoint. Unassigning brings the vehicle to an immediate, correct stop rather than continuing in a straight line. The vehicle now sits stationary from startup until first assigned, rather than drifting.
