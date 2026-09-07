# GBMS — Vehicle Configuration

**Status:** Proposed design.
**Scope:** Startup-time configuration of Own Vehicle's physical capabilities (speed range, turn rate, acceleration range) and its initial state for a given simulation run (vehicle type, starting position/heading), replacing the values currently hardcoded in `OwnVehicle`. Also introduces a general mechanism for surfacing startup configuration problems to the operator.

**Depends on:** `GBMS_Route_Assignment_Navigation_Design.md` — this doc extends `OwnVehicle`'s construction and its use of waypoint speed; it does not change the navigation algorithm itself.

---

## 1. Why this is needed

`OwnVehicle` currently hardcodes everything about the vehicle it represents:

```csharp
public OwnVehicle()
{
    Position = new Position(51.24708, -2.08829, 90.0);
}

public double MaxTurnRateDegreesPerSecond { get; set; } = 15.0;
```

GBMS is intended to simulate a number of different armoured vehicle types (Alvis Stormer, Boxer, Piranha V, and similar), and to support comparing how the same route or scenario behaves under different vehicles' physical limits. Neither is possible while those limits live as constants inside a single class with no notion of "which vehicle." This design externalises that data.

---

## 2. Two kinds of data, not one

Comparing vehicles meaningfully — "mix and match to see operational benefits" — means being able to vary *which vehicle type* is used without duplicating or hand-editing the vehicle's own specification each time, and without vehicle specifications and run-specific setup being tangled together in one file.

That splits into two things with genuinely different lifecycles:

- **Vehicle type definitions** — intrinsic, curated data describing a vehicle's physical capabilities. Rarely changes; a Boxer's speed range doesn't change between simulation runs.
- **Scenario selection** — which type is Own Vehicle *for this run*, and where it starts. Changes often; this is the thing you'd actually edit to try a different setup.

Putting both in one file forces a choice between duplicating the full vehicle spec for every variant you want to try, or building the same type/instance split inside that one file anyway — at which point the split has been reinvented with more code, not avoided. Keeping them as two separate concerns from the start avoids that, and reuses a pattern already established in this codebase for exactly this kind of problem — see §5.

---

## 3. Vehicle type definition

A new model, `VehicleType` (`GBMS.Models`):

```csharp
public class VehicleType
{
    public string VehicleTypeId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // Speeds in km/h, matching the existing waypoint speed convention.
    public double MinSpeedKmh { get; set; }
    public double MaxSpeedKmh { get; set; }

    // Fixed per vehicle type - see §6 for why this is not runtime-settable.
    public double MaxTurnRateDegreesPerSecond { get; set; }

    // Not yet used by any navigation logic - acceleration isn't modelled
    // yet (OwnVehicle currently applies target speed directly, with no
    // ramp). Captured now anyway, at the person's explicit request, since
    // it's an intrinsic part of a vehicle's physical specification and
    // costs nothing to store ahead of the model that will consume it.
    // Everything else in this design still adds fields only when
    // something reads them - this is a deliberate, narrow exception.
    public double MinAccelerationMetresPerSecondSquared { get; set; }
    public double MaxAccelerationMetresPerSecondSquared { get; set; }
}
```

One JSON file per type, human-named rather than auto-ID'd:

```json
// VehicleTypes/Boxer.json
{
  "VehicleTypeId": "Boxer",
  "DisplayName": "Boxer",
  "MinSpeedKmh": 0,
  "MaxSpeedKmh": 103,
  "MaxTurnRateDegreesPerSecond": 15.0,
  "MinAccelerationMetresPerSecondSquared": -3.0,
  "MaxAccelerationMetresPerSecondSquared": 2.0
}
```

This is the one deliberate divergence from the `Route` persistence pattern: routes are a runtime-growing collection needing auto-incrementing numeric IDs; vehicle types are a small, hand-curated, essentially fixed set that a person names directly. A string `VehicleTypeId` matching the filename is simpler and more legible than reusing route-style integer IDs for data that will never be created or renumbered by the running application.

---

## 4. Scenario / run selection

A single file, `Scenario.json`, describing what's active for a given run:

```json
{
  "VehicleTypeId": "Boxer",
  "StartLatitude": 51.24708,
  "StartLongitude": -2.08829,
  "StartHeading": 90.0
}
```

```csharp
public class ScenarioConfiguration
{
    public string VehicleTypeId { get; set; } = string.Empty;

    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double StartHeading { get; set; }
}
```

This is the file that actually changes between test runs — different starting position, different vehicle type — without ever touching the type library. Comparing two vehicle types from the same start point is two small `Scenario.json` variants against one shared, unmodified `VehicleTypes` folder.

---

## 5. Persistence, fallback, and startup alerting

Reuses the shape already established by `RoutePersistence` — a folder of JSON files, enumerated and validated on load, invalid entries logged and skipped rather than crashing startup:

```csharp
public interface IVehicleTypePersistence
{
    IReadOnlyList<VehicleType> LoadAll();
}
```

Deliberately **lighter** than `IRoutePersistence`: no `SaveNew`/`Save`/`Delete`. Vehicle types and the scenario file are hand-edited configuration, not data the running application creates, modifies, or deletes — there's no in-app editing UI for either (see §9), so there's nothing to save back.

The scenario file is a single object, not a collection, so it doesn't need even the `LoadAll` shape:

```csharp
public interface IScenarioPersistence
{
    ScenarioConfiguration Load();
}
```

### Default vehicle — a bundled JSON asset, not a hardcoded object

Rather than a `VehicleType.Default` constant embedded in C#, the fallback vehicle is its own JSON file, bundled with the application as an Avalonia asset — the same approach `TacticalSymbolResolver` already uses for tactical symbols that must always be present regardless of what's in the writable data folder:

```csharp
// avares://GBMS/Assets/Configuration/DefaultVehicleType.json
// avares://GBMS/Assets/Configuration/DefaultScenario.json
```

Loaded via `AssetLoader.Open(...)`, using the same `VehicleType`/`ScenarioConfiguration` deserialization as any other file — there is no special-cased in-code default object, just a guaranteed-present file read through a different loader.

### Surfacing startup problems to the operator — a pull-based startup alert list

Vehicle configuration is foundational, so problems here should be visible to the operator, not just written to the log. The natural place to show this is the existing MFD alarm bar (`MfdViewModel`'s `AlertText`/`IsAlertActive`, already fed by `Messenger` for live conditions such as route-deletion arming). But routing a *startup*-time problem through `Messenger` unmodified doesn't work: `Messenger.Send` is fire-and-forget, and `MfdViewModel` — the only subscriber — isn't constructed until after `ApplicationFactory.Initialize()` (where configuration loads) has already returned. A message sent during loading has no subscriber yet to receive it.

Rather than changing `Messenger` itself to buffer/replay messages — extra machinery inside a class that's otherwise a simple, general-purpose event bus, and machinery that only exists to serve this one narrow timing case — the simpler fix is to recognise that this isn't really a "live event" at all. It's a one-time condition discovered during startup, before any UI exists to consume it. That's a **pull**, not a **push**: instead of trying to fire an event at exactly the right moment, record what happened and let the first UI component that's ready go and read it.

`ApplicationFactory` already owns the entire startup sequence and is exactly where every configuration domain's loading happens — vehicle configuration today, potentially others later — so it's the natural place to hold this:

```csharp
public static class ApplicationFactory
{
    private static readonly List<AppMessage> _startupAlerts = new();

    /// <summary>
    /// Messages raised while ApplicationFactory.Initialize() was running,
    /// before any UI existed to display them - e.g. configuration
    /// fallbacks. Read once by the first UI component ready to show them
    /// (see MfdViewModel). Not domain-specific - any startup step can
    /// record here, not just vehicle configuration.
    /// </summary>
    public static IReadOnlyList<AppMessage> StartupAlerts => _startupAlerts;

    private static void RecordStartupAlert(string text, bool isAlert = true)
    {
        _startupAlerts.Add(new AppMessage(text, isAlert));

        if (isAlert)
            Logger.Warning(text);
        else
            Logger.Information(text);
    }

    // ... called from within Initialize() wherever a fallback occurs
}
```

`MfdViewModel`, once it exists and has subscribed to `Messenger.MessageReceived` as it already does today, replays anything waiting in `StartupAlerts` through the exact same handling logic used for live messages — no new display code, no second alert mechanism:

```csharp
_messenger.MessageReceived += OnMessageReceived;

foreach (AppMessage startupAlert in ApplicationFactory.StartupAlerts)
{
    OnMessageReceived(this, startupAlert);
}
```

This is smaller than the `Messenger` change it replaces — a plain list on the one class that already coordinates startup, no changes to `Messenger`'s event semantics at all — and it generalises exactly the way you'd want for "other configuration issues" going forward: any future startup step, in any domain, records into the same list via the same one-line call, with no per-domain plumbing and no dependency on when any particular UI component happens to be constructed relative to it.

### Fallback and alerting behaviour

- **`VehicleTypes` folder missing or empty** — `RecordStartupAlert(...)`, falls back to the bundled default vehicle type asset.
- **`Scenario.json` missing** — `RecordStartupAlert(...)`, falls back to the bundled default scenario asset.
- **`Scenario.json` references a `VehicleTypeId` not present in the loaded type library** — `RecordStartupAlert(...)` (this is a real misconfiguration, not an empty-on-first-run state, so it's worth keeping the message text distinct from the other two), falls back to the bundled default vehicle type.

---

## 6. `OwnVehicle` integration — configured limits are authoritative over route data

This is the key design decision in this document, worth stating explicitly rather than leaving implicit in the code: **`OwnVehicle.Speed` is the vehicle's actual physical speed at any given time. A waypoint's speed is a request, not a guarantee — it is always clamped to the vehicle's configured `[MinSpeedKmh, MaxSpeedKmh]` before being applied.**

This falls directly out of §2's route/vehicle split being genuinely independent: a route is persisted data with no knowledge of which vehicle will ever be assigned to it, and per the "mix and match" goal, the same route may legitimately be assigned to different vehicle types with different limits across different runs. It would be meaningless for a waypoint to hard-dictate a vehicle's speed regardless of what that vehicle is actually capable of — clamping has to happen on the vehicle side, because the vehicle is the only place that knows what its own limits are.

Concretely, in `UpdateNavigating` (see the navigation doc §1):

```csharp
// Waypoint speed is km/h; vehicle motion works in metres/second. The
// waypoint expresses a desired speed, not a guarantee - it is clamped to
// this vehicle's own configured limits, since the same route may be
// assigned to a different vehicle type with different capabilities on
// another run.
double requestedSpeedMetresPerSecond = target.Speed / 3.6;

Speed = Math.Clamp(
    requestedSpeedMetresPerSecond,
    _minSpeedMetresPerSecond,
    _maxSpeedMetresPerSecond);
```

`_minSpeedMetresPerSecond`/`_maxSpeedMetresPerSecond` are private fields set once at construction from the resolved `VehicleType`, converted from km/h the same way waypoint speed already is.

### The clamp does not apply to the stationary "hold" state

`Speed = 0` while unassigned or at route end (navigation doc §1) represents **not moving**, not "moving below minimum speed" — it is a distinct state from active navigation, not a violation of `MinSpeedKmh`. The clamp above only ever runs inside `UpdateNavigating`, so this is naturally already correct — flagged here so it's understood as a deliberate boundary, not an oversight if `MinSpeedKmh` is ever set above zero for a vehicle type.

### `MaxTurnRateDegreesPerSecond` — configuration-only, no runtime setter

**Decided:** `MaxTurnRateDegreesPerSecond` is set once at construction from the resolved `VehicleType` and is not publicly settable afterward. The only anticipated reason to vary it is testing different turn behaviour — and that's already exactly what editing a vehicle type's JSON file and restarting achieves, without needing a second, parallel way to change the same value at runtime. Removing the public setter also means a vehicle's turn rate can't drift from its configuration mid-run by accident, which a settable property always leaves open as a possibility even if nothing in the codebase currently exploits it.

### Acceleration limits — stored, not yet applied

`MinAccelerationMetresPerSecondSquared`/`MaxAccelerationMetresPerSecondSquared` are read from configuration and stored on `OwnVehicle`, but nothing in `Update`/`UpdateNavigating` uses them yet — per the navigation doc's existing constraint, waypoint speed is still applied directly with no ramp. Wiring them in is future work, once acceleration modelling is actually designed; storing them now just means that work won't require another configuration-loading change when it happens.

---

## 7. Startup wiring

Mirrors the existing `RouteManager` construction pattern in `ApplicationFactory.Initialize()`:

```csharp
var vehicleTypePersistence = new VehicleTypePersistence();
var scenarioPersistence = new ScenarioPersistence();

IReadOnlyList<VehicleType> vehicleTypes = vehicleTypePersistence.LoadAll();

if (vehicleTypes.Count == 0)
{
    RecordStartupAlert("No vehicle types configured - using default vehicle.");
    vehicleTypes = new[] { DefaultVehicleTypeAsset.Load() };
}

ScenarioConfiguration? scenario = scenarioPersistence.Load();

if (scenario == null)
{
    RecordStartupAlert("No scenario configured - using default scenario.");
    scenario = DefaultScenarioAsset.Load();
}

VehicleType? resolvedType =
    vehicleTypes.FirstOrDefault(t => t.VehicleTypeId == scenario.VehicleTypeId);

if (resolvedType == null)
{
    RecordStartupAlert(
        $"Unknown vehicle type '{scenario.VehicleTypeId}' - using default vehicle.");
    resolvedType = DefaultVehicleTypeAsset.Load();
}

_routeManager = new RouteManager(routePersistence);
_simulationManager = new SimulationManager(resolvedType, scenario);
```

`SimulationManager`'s constructor takes the resolved type and scenario, constructing `OwnVehicle` from them instead of `OwnVehicle`'s current parameterless constructor. This is the only change to `SimulationManager` — it already owns `OwnVehicle`'s construction, so no new component or ownership change is needed, just a different set of inputs to an existing constructor call.

No ordering dependency on `Messenger` or `MfdViewModel` exists here at all — `StartupAlerts` is a plain list read later, whenever the UI is ready for it.

---

## 8. Resolved decisions (this revision)

- **Turn rate is configuration-only.** No runtime setter; changing it means editing the vehicle type file. See §6.
- **Fallback is logged *and* alerted, and the fallback data itself is a real JSON file**, bundled as an Avalonia asset rather than a hardcoded object — consistent with how the application already guarantees other essential assets (tactical symbols) are always present regardless of the writable data folder's state. See §5.
- **Startup alerting uses a plain, pull-based list on `ApplicationFactory` (`StartupAlerts`), not a change to `Messenger`.** `MfdViewModel` replays it through the existing message-handling logic once it's subscribed. This avoids adding buffering/replay machinery to a class that's otherwise a simple general-purpose event bus, and generalises cleanly to configuration problems in domains other than vehicle configuration, since any startup step can record into the same list. See §5.

---

## 9. Constraints carried / explicitly deferred

Carried forward from the existing architecture docs:
- No new persistence framework — this reuses the existing folder-of-JSON-files pattern, narrowed to read-only where that's all that's needed (§5).
- No speculative abstraction ahead of a concrete requirement.
- Configuration remains hand-edited files, consistent with how routes are already managed outside the running application's write path for anything other than routes themselves.
- `Messenger` remains unchanged — it stays a simple, general live-event bus; startup-specific handling lives in `ApplicationFactory` instead (§5).

Explicitly deferred, not built now:
- **Multiple simultaneous vehicles.** This design resolves one vehicle type for one `OwnVehicle` instance per run. A registry or selection mechanism for *concurrently* running multiple vehicles is a materially different feature with no current requirement.
- **Launcher/missile/weapon configuration.** No weapon or launcher model exists anywhere in the codebase yet (WPN/STR are still placeholder functional areas). Extending `VehicleType` to cover them now would be designing a shape for data that doesn't exist.
- **In-app configuration editing UI.** Vehicle type and scenario files are edited directly, the same way route JSON files can be today, outside the running application.
- **Acceleration modelling.** The values are captured (§3, §6) but not applied — modelling ramped speed change is separate future work.
- **A generalised, multi-domain "application configuration" system.** Vehicle configuration is the concrete need that exists today. `StartupAlerts` (§5) is deliberately just a flat list, not a categorised or structured diagnostics framework — if startup problems grow varied enough to need more than "a message and a severity," that's worth its own design at that point rather than anticipated now.

---

## 10. Suggested implementation order

1. Add `VehicleType` and `ScenarioConfiguration` models, plus `IVehicleTypePersistence`/`IScenarioPersistence` and their file-backed implementations. Verify loading in isolation (no `OwnVehicle`/`ApplicationFactory` wiring yet) against a couple of hand-written sample files.
2. Add the bundled default vehicle type and default scenario JSON assets, and the loaders that read them via `AssetLoader` (mirroring `TacticalSymbolResolver`).
3. Add `ApplicationFactory.StartupAlerts` and `RecordStartupAlert(...)` (§5). Wire `MfdViewModel` to replay it after subscribing to `Messenger`. Verify with a manual test: force a startup alert to be recorded, confirm it appears in the alarm bar on first render with no live message having been sent.
4. Add the fallback logic from §5/§7 (missing folder, missing scenario file, unresolved type ID), and verify each case logs, records a startup alert, and still produces a usable default.
5. Extend `OwnVehicle`'s construction to accept a resolved `VehicleType` and starting position/heading, replacing the hardcoded constructor values. Remove the public setter on `MaxTurnRateDegreesPerSecond`. Add the speed-clamp from §6 to `UpdateNavigating`.
6. Wire `ApplicationFactory.Initialize()` and `SimulationManager`'s constructor per §7.
7. Manually test: run with no `VehicleTypes`/`Scenario.json` present — confirm the default vehicle loads, the alarm bar shows the fallback alert, and the vehicle still behaves correctly; add one real vehicle type and a matching scenario file and confirm it's used instead; assign a route with waypoint speeds both inside and outside the configured vehicle's range and confirm `OwnVehicle.Speed` is clamped, never exceeding or dropping below the configured limits during active navigation; swap `Scenario.json` to a second vehicle type against the same route and confirm behaviour changes accordingly with no changes to the route or the type library.
