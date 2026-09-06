# GBMS — Route Assignment

**Status:** Proposed design (increment on top of MVP Tactical Map / Route Management baseline)
**Scope:** The act of assigning `RouteManager.CurrentRoute` to Own Vehicle. Navigation behaviour is explicitly out of scope — see `GBMS_Route_Assignment_Navigation_Design.md`, which now depends on this doc rather than the reverse.

---

## 1. Why this is its own step

`CurrentRoute` already exists and is set via the established Route Manager flow (L3 open → L5/L6 cursor → L4 select). But nothing today records "this is the route Own Vehicle is assigned to." That fact needs to exist, be settable, be observable, and survive edge cases (deletion, re-assignment) — independently of whether anything downstream acts on it yet. Building navigation before this exists would be untestable, since there'd be nothing real to navigate against.

---

## 2. Where assignment state lives

The base architecture doc already anticipates this:

> "Future responsibility [of RouteManager] includes assignment of a route to Own Vehicle."

So this isn't a new component — it's a direct extension of `RouteManager`, consistent with the MVP/KISS constraint to reuse existing services rather than introduce a new one. `RouteManager` already owns one route-selection concept (`CurrentRoute`); assignment is a second, deliberately distinct one, following the exact same shape.

### `RouteManager` additions (as implemented)
- `AssignedRoute` (nullable `Route`) — the route currently assigned to Own Vehicle. Independent of `CurrentRoute`.
- `AssignRoute(int routeId)` — matches `SelectRoute(int routeId)`'s existing pattern exactly: looks the route up by ID, throws if it doesn't exist, no-ops if already assigned, otherwise sets `AssignedRoute` and raises `AssignedRouteChanged`. Takes an ID rather than a `Route` reference so it gets the same existence/ownership validation every other mutating method here already performs.
- `AssignedRouteChanged` — new event, same shape/purpose as the existing `CurrentRouteChanged`.

`RouteManager` still knows nothing about MFD, navigation, or vehicle kinematics. It just holds a second pointer and announces when it changes — exactly what it already does for `CurrentRoute`.

### Relationship between `CurrentRoute` and `AssignedRoute`
These are deliberately independent:
- `CurrentRoute` = "the route currently displayed/being managed" (existing).
- `AssignedRoute` = "the route Own Vehicle is assigned to" (new).

A route can be `CurrentRoute` without being `AssignedRoute` (browsing), and `AssignedRoute` without being `CurrentRoute` (assigned earlier, user has since browsed elsewhere). This mirrors the doc's own established distinction between Route Manager cursor state and actual selection — same pattern, one level up.

---

## 3. MFD wiring

As you noted, L4 in the Normal Tactical Map context is the natural fit — it's currently unassigned there:

| Button | Normal Tactical Map |
|---|---|
| L4 | Assign `CurrentRoute` to Own Vehicle |

Behaviour:
- Pressing L4 calls `RouteManager.AssignRoute(CurrentRoute)`.
- Enabled only when `CurrentRoute != null` and `CurrentRoute != AssignedRoute` (nothing to do otherwise — avoids a redundant re-assign).
- Only active in the Normal Tactical Map context, consistent with L4 meaning something else in Route Manager (select) and Route Editing (speed decrease) contexts already.

### Flow

```text
L4 (Normal Tactical Map, CurrentRoute set and differs from AssignedRoute)
  → TacticalMapViewModel
  → AssignRouteRequested event
  → TacticalMapView
  → RouteManager.AssignRoute(RouteManager.CurrentRoute.Id)
  → AssignedRouteChanged
  → TacticalMapView
  → contextual icon / indicator updates
```

This is structurally identical to the existing `CurrentRouteChanged` flow — same event pattern, same presentation boundary, just a different fact being communicated.

---

## 4. Presentation implications (decided)

**Decision: Option 2 — distinct visual style when `CurrentRoute == AssignedRoute`.**

Rationale: once a route is assigned, Route Manager is rarely revisited in normal operation — the main reason to return to it is realising the assigned route is wrong and reassigning. Given that, a persistent independent "always show assigned route" layer (Option 3) would add real rendering complexity for a scenario that's the exception, not the norm. But *some* visual confirmation still matters — the simulation is centred on Own Vehicle, so knowing what it's assigned to, and being able to confirm assignment succeeded, is operationally important. Option 2 covers this: confirmation is visible at the moment of assignment, and again on the rare re-check, without a second rendering layer.

### Implementation implication (finalised)
`RouteLayer.SetRoute(Route? route, bool isAssigned)` — a single method, not a separate `SetAssigned`. `TacticalMapView` passes both pieces of state together every time either `CurrentRouteChanged` or `AssignedRouteChanged` fires, computing `isAssigned` by comparing `CurrentRoute` to `AssignedRoute`:

```csharp
RouteLayer.SetRoute(
    RouteManager.CurrentRoute,
    isAssigned: RouteManager.CurrentRoute == RouteManager.AssignedRoute);
```

**Invariant:**
- `TacticalMapView` owns the relationship between `CurrentRoute` and `AssignedRoute` — it decides, on every relevant event, what the complete presentation state should be.
- `RouteLayer` only renders the complete state it is given. It holds no memory of a previously-set route or assignment flag between calls — every call is a full statement of what to draw and how.

This avoids `RouteLayer` needing any internal state to stay in sync with, and removes any ordering dependency between "set the route" and "set the assignment" calls (there's only one call).

**Explicitly not needed for this:**
- A second route layer.
- A second copy of the route (assignment is a reference to the same `Route` object `RouteManager` already owns — no duplication).
- A `RouteAssignmentViewModel` or any new ViewModel.
- A complex route-state system (no state machine — it's one boolean derived by comparison, computed fresh each time either event fires).
- Navigation infrastructure — still entirely out of scope for this increment.

---

## 5. Edge cases

### Deleting the assigned route
`RemoveRoute` already guards against deleting `CurrentRoute` by throwing `InvalidOperationException` rather than clearing it — a stricter rule than originally assumed in this doc. For consistency, `AssignedRoute` follows the identical pattern: attempting to remove the currently-assigned route throws, same as attempting to remove the currently-displayed one. Two independent guard clauses, same shape, since a route could be `CurrentRoute`, `AssignedRoute`, both, or neither at time of deletion.

### Re-assignment
Assigning a different route while one is already assigned is just a normal `AssignRoute` call — `AssignedRoute` is overwritten, `AssignedRouteChanged` fires once with the new value. No special-case logic needed; this is exactly why `AssignedRoute` is a plain nullable reference rather than something with its own state machine.

### Assigning with no `CurrentRoute` set
Not reachable if L4 is correctly disabled per §3, but if `AssignRoute(null)` is ever called defensively, it should simply be a no-op (or clear the assignment, if you decide that's a desirable manual "un-assign" behaviour — see below).

### Un-assigning
Not requested, and not required for this increment — flagging only so it's a conscious omission rather than an oversight. If needed later, it likely wants its own explicit control rather than overloading L4.

---

## 6. Constraints carried

- `RouteManager` remains the sole owner of route domain data, including now both selection pointers (`CurrentRoute`, `AssignedRoute`). It still has zero knowledge of MFD, map rendering, or vehicle kinematics.
- `TacticalMapView` forwards the assign request; it does not hold or compute assignment state itself.
- No new component introduced — this extends `RouteManager` using the same pattern it already uses for `CurrentRoute`.
- No navigation logic included or implied by this increment — `AssignedRoute` is inert data until the navigation design is built on top of it.

---

## 7. Suggested implementation order

1. Add `AssignedRoute` and `AssignedRouteChanged` to `RouteManager`, plus `AssignRoute(route)`. Verify with a direct unit-level call (no MFD wiring yet) that assignment and the event fire correctly.
2. Add the deletion-clears-assignment check to the existing delete path.
3. Wire L4 in the Normal Tactical Map context, including the enabled/disabled condition.
4. Add whatever minimal visual confirmation you decide on from §4 (or explicitly defer it).
5. Manually test: select route → close Route Manager → assign via L4 → confirm `AssignedRoute` is set and icon/enablement reflects it → delete the assigned route → confirm it clears correctly.

Once this is solid and tested in isolation, the navigation design can consume `RouteManager.AssignedRoute` / `AssignedRouteChanged` as its input, exactly the way `TacticalMapView` already consumes `CurrentRouteChanged`.
