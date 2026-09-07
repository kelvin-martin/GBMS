# GBMS — Route Assignment

**Status:** Implemented and tested.
**Scope:** Assigning and un-assigning `RouteManager.CurrentRoute`/`AssignedRoute` for Own Vehicle, including the Route Manager availability rules that govern when assignment/unassignment can occur. Navigation behaviour remains out of scope — see `GBMS_Route_Assignment_Navigation_Design.md`, which depends on this doc.

---

## 1. Why this is its own step

`CurrentRoute` already exists and is set via the established Route Manager flow (L3 open → L5/L6 cursor → L4 select). But nothing originally recorded "this is the route Own Vehicle is assigned to." That fact needs to exist, be settable, be observable, and survive edge cases (deletion, re-assignment, un-assignment) — independently of whether anything downstream acts on it. Building navigation before this existed would have been untestable, since there'd have been nothing real to navigate against.

---

## 2. Where assignment state lives

This is a direct extension of `RouteManager`, consistent with the MVP/KISS constraint to reuse existing services rather than introduce a new one. `RouteManager` already owns one route-selection concept (`CurrentRoute`); assignment is a second, deliberately distinct one, following the same shape.

### `RouteManager` additions

- `AssignedRoute` (nullable `Route`) — the route currently assigned to Own Vehicle. Independent of `CurrentRoute` in general, though see §4 for the invariant that holds while an assignment is active.
- `AssignRoute(int routeId)` — matches `SelectRoute(int routeId)`'s pattern: looks the route up by ID, throws if it doesn't exist, no-ops if already assigned, otherwise sets `AssignedRoute` and raises `AssignedRouteChanged`.
- `AssignedRouteChanged` — event, same shape/purpose as `CurrentRouteChanged`.
- `ClearAssignedRoute()` — **new**. Un-assigns Own Vehicle's route, allowing a different route to be selected and assigned. No-ops if nothing is assigned.

```csharp
public void ClearAssignedRoute()
{
    if (AssignedRoute == null)
        return;

    AssignedRoute = null;
    AssignedRouteChanged?.Invoke(null);

    ClearCurrentRoute();
}
```

Calling `ClearCurrentRoute()` internally is deliberate: it keeps "assigned and displayed are the same route" true right up to the moment of unassignment, so the map returns to blank in the same action rather than leaving a stale route on screen that the operator has to separately dismiss.

`RouteManager` still knows nothing about MFD, navigation, or vehicle kinematics.

### Relationship between `CurrentRoute` and `AssignedRoute`

- `CurrentRoute` = "the route currently displayed/being managed."
- `AssignedRoute` = "the route Own Vehicle is assigned to."

Historically these were independent — a route could be `CurrentRoute` without being `AssignedRoute` (browsing), and `AssignedRoute` without being `CurrentRoute` (assigned earlier, browsed elsewhere since). **This is no longer possible while a route is assigned** — see §4 for the invariant that now holds, and the design history behind it.

---

## 3. MFD wiring

L4 in Normal Tactical Map has three mutually-exclusive behaviours, selected by state:

| Button | Normal Tactical Map |
|---|---|
| L3 | Show/hide Route Manager — **only available when no route is assigned** |
| L4 | Assign `CurrentRoute` to Own Vehicle, **or** unassign the current assignment — whichever applies |

```csharp
case MfdFunctionKey.L4:
    if (RouteManagerControl.IsDisplayed)
    {
        // Handled entirely within Route Manager - select or arm/confirm
        // delete. See the Tactical Map Architecture doc.
        RouteManagerControl.RequestAcceptOrDelete();
        UpdateContextualIcons();
    }
    else if (_routeManager?.AssignedRoute != null)
    {
        _routeManager.ClearAssignedRoute();
        UpdateContextualIcons();
    }
    else if (_routeManager?.CurrentRoute != null)
    {
        _routeManager.AssignRoute(_routeManager.CurrentRoute.Id);
        UpdateContextualIcons();
    }
    break;
```

Every branch that changes assignment, selection, or Route Manager's displayed/pending state calls `UpdateContextualIcons()` before returning — icon visibility here is entirely derived state with no other refresh trigger, and every branch omitting this call was a real bug caught during testing (the unassign icon failing to appear after assigning, because the assign branch alone wasn't refreshing icons).

L3 gating:

```csharp
case MfdFunctionKey.L3:
    if (_routeManager?.AssignedRoute == null)
    {
        RouteManagerControl.Show(!RouteManagerControl.IsDisplayed);
        UpdateContextualIcons();
    }
    break;
```

L3 is a silent no-op while a route is assigned. The Route Manager icon itself is hidden in that state (see the architecture doc's contextual icons section), so there's no live control inviting a press that would do nothing.

### Flow — assign

```text
L4 (Normal Tactical Map, Route Manager hidden, CurrentRoute set, AssignedRoute == null)
  → TacticalMapView
  → RouteManager.AssignRoute(CurrentRoute.Id)
  → AssignedRouteChanged
  → TacticalMapView
  → RouteLayer restyled blue, contextual icons refreshed (assign icon → unassign icon)
```

### Flow — unassign

```text
L4 (Normal Tactical Map, Route Manager hidden, AssignedRoute != null)
  → TacticalMapView
  → RouteManager.ClearAssignedRoute()
  → AssignedRouteChanged(null), then CurrentRouteChanged(null)
  → TacticalMapView
  → RouteLayer cleared, contextual icons refreshed, Route Manager icon reappears
```

---

## 4. Route Manager availability while a route is assigned (design decision)

**Decision: Route Manager cannot be displayed while a route is assigned to Own Vehicle. To browse or select a different route, the operator must unassign first.**

### Why

Route Manager's purpose is to browse the persisted route library and pick one for display/eventual assignment. Once a route is assigned, navigation is underway — there's no legitimate reason to be browsing the library at that point, and allowing it invites the exact problem this section addresses: `CurrentRoute` (freely changeable via Route Manager) and `AssignedRoute` (what Own Vehicle is actually following) drifting apart from one another.

### History — an alternative was built, tested, and deliberately rolled back

An earlier iteration handled the same underlying problem differently: rather than locking Route Manager out, `RouteLayer` was duplicated onto a second, independent map layer (`_assignedRouteLayer`) that always rendered `AssignedRoute` regardless of what Route Manager currently had selected for display. This worked and was tested successfully — the assigned route stayed visible at all times, and browsing other routes no longer made it disappear from the map.

It was rolled back once the mutual-exclusion approach below was agreed, because the two approaches solve the same problem at different points: the second-layer approach let `CurrentRoute` and `AssignedRoute` diverge and then compensated for it at render time; the mutual-exclusion approach prevents the divergence from being possible at all. The latter is simpler — one map layer, no reference-comparison logic needed for rendering purposes beyond the existing `IsAssigned` check, and no scenario where Route Manager could ever be showing something Own Vehicle isn't actually assigned to. The second-layer code (the extra `RouteLayer` instance, its wiring into `OnCurrentRouteChanged`/`OnAssignedRouteChanged`) was fully removed as part of the rollback.

### The resulting invariant

**While `AssignedRoute != null`, `CurrentRoute == AssignedRoute` always holds.**

This falls directly out of the reachability rules in §3: the only way to change `CurrentRoute` is via Route Manager selection (locked out while assigned) or by assigning `CurrentRoute` itself (which makes it equal to `AssignedRoute` immediately, by definition). The only way to break the equality is `ClearAssignedRoute()`, which clears both sides in the same call. There is no code path that can set `CurrentRoute` to something other than `AssignedRoute` while an assignment is active.

**Consequence — route editing while assigned.** `RouteEditor.CurrentRoute` is kept in sync with `RouteManager.CurrentRoute` on every `CurrentRouteChanged` event, so while a route is assigned, `RouteEditor.CurrentRoute` is also always that same route. The L2 route-creation gesture resumes editing whatever `RouteEditor.CurrentRoute` currently holds rather than starting blank — so while a route is assigned, L2 always edits the assigned route; it cannot be used to start a genuinely new one. This is treated as expected behaviour, not a gap: the shared-reference mechanism is exactly what makes editing the assigned route live-update navigation (see the navigation doc's resolved "editing the assigned route while navigating" section), and there's no spare contextual button available to disambiguate "edit this" from "start fresh." Building a new route requires unassigning first (L4), after which `CurrentRoute` is `null` and L2 correctly starts blank.

---

## 5. Presentation

**Decision (unchanged from the original choice): a route is drawn blue when it is the assigned route, red otherwise.**

`RouteLayer.SetRoute(Route? route, bool isAssigned)` remains a single call, with `TacticalMapView` computing `isAssigned` by comparing `CurrentRoute` to `AssignedRoute`:

```csharp
private bool IsAssigned(Route? route) =>
    route != null && ReferenceEquals(route, _routeManager?.AssignedRoute);
```

What changed is not the mechanism but the scenario it has to handle. Previously `CurrentRoute` and `AssignedRoute` could genuinely differ (browse route A while route B stays assigned), so `IsAssigned` existed to answer "is *this particular* route the assigned one, given they might not match." Now, per §4's invariant, whenever `AssignedRoute != null` the two are never anything but the same object — the flag is still computed the same way, but the comparison it evaluates is structurally fixed for the duration of any assignment rather than something that could go either way.

Still explicitly not needed: a second route layer, a second copy of the route, a `RouteAssignmentViewModel`, a state machine.

---

## 6. Edge cases

### Deleting the assigned route
Unchanged: `RemoveRoute` throws for `CurrentRoute` and `AssignedRoute` independently. Given §4's invariant, whenever a route is assigned these are now the same route hit by both guards under one condition — but both checks remain, since a route can still be `CurrentRoute` without being `AssignedRoute` while nothing is assigned (ordinary browsing/editing). In practice, deletion of the assigned route is additionally unreachable through the UI, since Route Manager — the only place deletion is triggered from — can't be open while a route is assigned (§4). The domain-layer guard remains in place regardless, as a defensive check rather than the primary safeguard.

### Re-assignment
Assigning a different route can now only happen when nothing is currently assigned (§4) — "re-assign without unassigning first" is not a reachable scenario by design, not merely an untested one. Once nothing is assigned, assigning a different route is an ordinary `AssignRoute` call as before.

### Assigning with no `CurrentRoute` set
Not reachable — L4's assign branch is guarded on `CurrentRoute != null`.

### Un-assigning — implemented
`ClearAssignedRoute()` (§2) is the explicit control this section originally flagged as a future requirement. It:
- Clears `AssignedRoute` and raises `AssignedRouteChanged(null)`.
- Immediately also clears `CurrentRoute` via `ClearCurrentRoute()`, returning the map to blank and re-enabling Route Manager (§4) in the same action.
- Does **not** stop Own Vehicle by itself — see the navigation doc for what happens to vehicle motion at the moment of unassignment (it now holds stationary, having previously reverted to a placeholder motion that has since been removed).

---

## 7. Constraints carried

- `RouteManager` remains the sole owner of route domain data — both selection pointers, and the unassign operation.
- `TacticalMapView` forwards requests; it does not hold or compute assignment state itself, beyond the `IsAssigned` comparison used purely for rendering.
- No new component introduced.
- Route Manager is unavailable while a route is assigned (§4) — a hard interaction constraint, not just a presentation choice.
- No navigation logic included or implied by this doc — see the navigation doc for vehicle behaviour.

---

## 8. Implementation history

1. ~~Add `AssignedRoute`, `AssignedRouteChanged`, `AssignRoute`~~ — done, unit-tested in isolation.
2. ~~Add deletion guards for `AssignedRoute`~~ — done.
3. ~~Wire L4 assign in Normal Tactical Map~~ — done.
4. ~~Add visual confirmation (blue/red)~~ — done (§5).
5. ~~Prototype an independent "always visible" assigned-route layer~~ — built, tested working, then rolled back in favour of §4's mutual-exclusion design (see §4 History).
6. ~~Lock Route Manager while assigned (L3 gating)~~ — done.
7. ~~Add `ClearAssignedRoute` and wire to L4 unassign~~ — done, including an icon-refresh fix (the unassign icon wasn't appearing after assigning until `UpdateContextualIcons()` was added consistently to every state-changing L4 branch, not just some of them).
8. ~~Auto-select newly created routes on exiting Route Editor~~ — done; see the Tactical Map Architecture doc's Route Manager interaction section. Deliberately does **not** auto-assign — assignment remains a separate, explicit action so multiple routes can be built in one session before any of them is committed to Own Vehicle.

Manually tested end-to-end: build a route → auto-selected → L4 assigns → Route Manager becomes unavailable (L3 no-ops, its icon hidden) → L4 unassigns → map returns to blank, Route Manager available again → repeat with a different route.
