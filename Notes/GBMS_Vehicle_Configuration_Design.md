# GBMS — Vehicle Configuration

**Status:** Implemented and tested.
**Scope:** Startup-time configuration of Own Vehicle's physical capabilities (speed range, turn rate, acceleration range) and its initial state for a given simulation run (vehicle type, starting position/heading), replacing the values previously hardcoded in `OwnVehicle`. Also introduces a general, pull-based mechanism for surfacing startup configuration problems to the operator.

**Depends on:** `GBMS_Route_Assignment_Navigation_Design.md` — this doc extends `OwnVehicle`'s construction and its use of waypoint speed; it does not change the navigation algorithm itself.

---

## 1. Why this was needed

`OwnVehicle` previously hardcoded everything about the vehicle it represented:

```csharp
public OwnVehicle()
{
    Position = new Position(51.24708, -2.08829, 90.0);
}

public double MaxTurnRateDegreesPerSecond { get; set; } = 15.0;
```

GBMS is intended to simulate a number of different armoured vehicle types, and to support comparing how the same route or scenario behaves under different vehicles' physical limits. Neither was possible while those limits lived as constants inside a single class with no notion of "which vehicle." This work externalised that data.

---

## 2. Two kinds of data, not one

Comparing vehicles meaningfully — "mix and match to see operational benefits" — means being able to vary *which vehicle type* is used without duplicating or hand-editing the vehicle's own specification each time, and without vehicle specifications and run-specific setup being tangled together in one file.

That splits into two things with genuinely different lifecycles:

- **Vehicle type definitions** — intrinsic, curated data describing a vehicle's physical capabilities. Rarely changes; a given type's speed range doesn't change between simulation runs.
- **Scenario selection** — which type is Own Vehicle *for this run*, and where it starts. Changes often; this is the thing actually edited to try a different setup.

Putting both in one file would force a choice between duplicating the full vehicle spec for every variant, or reinventing the same type/instance split inside that one file anyway. Keeping them as two separate concerns avoided that, and reused a pattern already established in this codebase — see §5.

---

## 3. Vehicle type definition

`VehicleType` (`GBMS.Models`):

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
    // yet (OwnVehicle applies target speed directly, with no ramp).
    // See §8a - both this and MaxTurnRateDegreesPerSecond are, for now,
    // placeholder values pending a higher-fidelity vehicle dynamics model,
    // not individually-sourced specifications.
    public double MinAccelerationMetresPerSecondSquared { get; set; }
    public double MaxAccelerationMetresPerSecondSquared { get; set; }
}
```

One JSON file per type, human-named rather than auto-ID'd, `VehicleTypeId` matching the filename exactly (enforced by `VehicleTypePersistence`, see §5). **Serialization uses `camelCase` keys**, matching the convention already established by `RoutePersistence` — the design doc's earlier illustrative snippets showed `PascalCase` for readability, but on-disk files use `camelCase`, exactly as route files do:

```json
// VehicleTypes/AlvisStormer.json
{
  "vehicleTypeId": "AlvisStormer",
  "displayName": "Alvis Stormer",
  "minSpeedKmh": 0,
  "maxSpeedKmh": 80,
  "maxTurnRateDegreesPerSecond": 15.0,
  "minAccelerationMetresPerSecondSquared": -3.0,
  "maxAccelerationMetresPerSecondSquared": 2.0
}
```

This remains the one deliberate divergence from the `Route` persistence pattern: routes are a runtime-growing collection needing auto-incrementing numeric IDs; vehicle types are a small, hand-curated, essentially fixed set that a person names directly. A string `VehicleTypeId` matching the filename is simpler and more legible than reusing route-style integer IDs for data the running application never creates or renumbers.

---

## 4. Scenario / run selection

`ScenarioConfiguration` (`GBMS.Models`), one file, `Scenario.json`:

```csharp
public class ScenarioConfiguration
{
    public string VehicleTypeId { get; set; } = string.Empty;

    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double StartHeading { get; set; }
}
```

```json
{
  "vehicleTypeId": "AlvisStormer",
  "startLatitude": 51.24708,
  "startLongitude": -2.08829,
  "startHeading": 90.0
}
```

This is the file that actually changes between test runs — different starting position, different vehicle type — without ever touching the type library.

---

## 5. Persistence, fallback, and startup alerting

`VehicleTypePersistence`/`IVehicleTypePersistence` and `ScenarioPersistence`/`IScenarioPersistence` mirror `RoutePersistence`'s validate-on-load, skip-invalid shape, deliberately **lighter** than `IRoutePersistence` — no `SaveNew`/`Save`/`Delete`, since vehicle types and the scenario file are hand-edited configuration with no in-app editing UI (§9).

**`VehicleTypePersistence.LoadAll()`** enumerates the `VehicleTypes` folder, and for each file validates:
- `VehicleTypeId` is present, and matches the filename exactly.
- `DisplayName` is present.
- `MinSpeedKmh`/`MaxSpeedKmh` are finite, non-negative, and `Max >= Min`.
- `MaxTurnRateDegreesPerSecond` is finite and positive.

Acceleration fields are deliberately **not** validated — nothing consumes them yet, so there's no failure mode for a bad value to trigger (see §8a).

**`ScenarioPersistence.Load()`** returns `null` on a missing or invalid file (never throws), validating:
- `VehicleTypeId` is present.
- `StartLatitude`/`StartLongitude` are within valid ranges.
- `StartHeading` is finite **and within `[0, 360)`** — an out-of-range heading (e.g. `999.0`) was initially accepted by a first pass of this check that only tested for `NaN`/`Infinity`; caught during testing and fixed to enforce the proper range, the same way latitude/longitude already were.

### Default vehicle — a bundled JSON asset

The fallback vehicle type and scenario are their own JSON files, bundled as Avalonia assets via `DefaultConfigurationAssets`, loaded through `AssetLoader` — the same approach `TacticalSymbolResolver` uses for tactical symbols that must always be present regardless of the writable data folder's state:

```
avares://GBMS/Assets/Configuration/DefaultVehicleType.json
avares://GBMS/Assets/Configuration/DefaultScenario.json
```

Unlike the user-facing persistence classes, a missing or malformed bundled asset **throws** (`FileNotFoundException`/`InvalidOperationException`) rather than degrading gracefully — its absence means the build/packaging is broken, not that the user misconfigured something, and that distinction was confirmed deliberately during testing (a temporarily-renamed asset file correctly threw rather than silently falling through further).

### Surfacing startup problems — a pull-based startup alert list

Configuration problems are recorded via `ApplicationFactory.RecordStartupAlert(string, bool)` into a plain list, `ApplicationFactory.StartupAlerts`, rather than pushed live through `Messenger`. This was a deliberate simplification made during design: `Messenger.Send` is fire-and-forget, and `MfdViewModel` — the only subscriber — doesn't exist yet at the point configuration loads inside `ApplicationFactory.Initialize()`. Buffering/replaying inside `Messenger` itself was considered and rejected as unnecessary machinery for a one-time startup condition; instead, `MfdViewModel` reads `ApplicationFactory.StartupAlerts` once, immediately after subscribing to `Messenger`, and replays each entry through its existing message-handling logic:

```csharp
_messenger.MessageReceived += OnMessageReceived;

foreach (AppMessage startupAlert in ApplicationFactory.StartupAlerts)
{
    OnMessageReceived(this, startupAlert);
}
```

No changes were needed to `Messenger` itself. This generalises to configuration problems in domains other than vehicle configuration — any future startup step can call the same `RecordStartupAlert(...)`, regardless of when any particular UI component happens to be constructed relative to it.

One confirmed characteristic, not a defect: if more than one alert is recorded in the same run, `AlertText`/`IsAlertActive` — single current-value properties, not a queue — will only visibly show the **last** one replayed. This matches the alert bar's existing single-message design for live messages and was accepted as-is rather than extended into a multi-message display.

### Fallback and alerting behaviour, as implemented

- **`VehicleTypes` folder missing or empty** → `RecordStartupAlert("No valid vehicle types found - using default vehicle.")`, falls back to the bundled default vehicle type.
- **`Scenario.json` missing or invalid** → `RecordStartupAlert("No valid scenario configuration found - using default scenario.")`, falls back to the bundled default scenario **and, directly, the bundled default vehicle type** — see the bug note below.
- **`Scenario.json` (validly loaded) references a `VehicleTypeId` not present in the loaded type library** → `RecordStartupAlert("Unknown vehicle type '{id}' - using default vehicle.")`, falls back to the bundled default vehicle type.

### Bug found and fixed during testing: spurious double alert on scenario fallback

Initial resolution logic ran the *same* lookup against the loaded `vehicleTypes` list regardless of whether `scenario` was the user's own file or the bundled default. Since the bundled default scenario references the bundled default vehicle type by an ID (`"Default"`) that was never going to appear in a user's own type library, this produced a spurious second alert — "Unknown vehicle type 'Default'" — stacked on top of the correct "No valid scenario configuration found" alert, every time the scenario itself fell back.

Fixed by recognising the default scenario and default vehicle type as a matched pair rather than two independently-resolved values: when `scenario` itself defaults, `resolvedVehicleType` is resolved **directly** from `DefaultConfigurationAssets`, bypassing the library lookup entirely, rather than searching a library that was never going to contain a match for it. Confirmed via testing that this fix:
- Eliminated the false positive (single alert only, when the scenario file alone was invalid).
- Left the genuinely informative double-alert case intact — an empty vehicle type library **and** a validly-shaped scenario naming a real-looking but absent ID (e.g. `"Boxer"` with no matching file) still correctly produces both alerts together, since that's two independently true and useful facts, not one restated twice.

---

## 6. `OwnVehicle` integration — configured limits are authoritative over route data

The key design decision underlying this feature: **`OwnVehicle.Speed` is the vehicle's actual physical speed at any given time. A waypoint's speed is a request, not a guarantee — it is always clamped to the vehicle's configured `[MinSpeedKmh, MaxSpeedKmh]` before being applied.**

This follows directly from §2's route/vehicle split being genuinely independent: a route has no knowledge of which vehicle will ever be assigned to it, and the same route may legitimately be assigned to different vehicle types with different limits across different runs. Clamping has to happen on the vehicle side, since the vehicle is the only place that knows its own limits.

As implemented, in `UpdateNavigating`:

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

`_minSpeedMetresPerSecond`/`_maxSpeedMetresPerSecond` are private fields set once at construction from the resolved `VehicleType`, converted from km/h the same way waypoint speed already is. **Confirmed via testing**: a route with a waypoint speed exceeding the configured vehicle's maximum was correctly clamped during active navigation, verified via temporary debug logging of `Speed` each tick.

### The clamp does not apply to the stationary "hold" state

`Speed = 0` while unassigned or at route end represents **not moving**, not "moving below minimum speed" — a distinct state from active navigation, not a violation of `MinSpeedKmh`. The clamp only ever runs inside `UpdateNavigating`, so this was already correct by construction.

### `MaxTurnRateDegreesPerSecond` — configuration-only, no runtime setter

**As implemented:** set once at construction from the resolved `VehicleType`, with no public setter. The only anticipated reason to vary it — testing different turn behaviour — is already achieved by editing a vehicle type's JSON file and restarting, without needing a second, parallel way to change the same value at runtime. **Confirmed via a project-wide search during testing** that nothing else in the codebase was assigning to this property from outside the constructor.

### Acceleration limits — stored, not yet applied

Read from configuration and stored on `OwnVehicle`, but nothing in `Update`/`UpdateNavigating` uses them — waypoint speed is still applied directly with no ramp, per the navigation doc's existing constraint. See §8a for the decision to treat both this and turn rate calibration as deferred to a future modelling pass, rather than something to refine now.

---

## 7. Startup wiring

`ApplicationFactory.Initialize()`, in order:

1. Construct `Messenger` (unchanged, first as before).
2. Load vehicle types via `VehicleTypePersistence.LoadAll()`; fall back to the bundled default (with alert) if empty.
3. Load the scenario via `ScenarioPersistence.Load()`; if `null`, fall back to the bundled default scenario **and** resolve the vehicle type directly from the bundled default (with alert) — see the bug fix in §5, not via the general lookup.
4. Otherwise, resolve `scenario.VehicleTypeId` against the loaded `vehicleTypes`; if unresolved, fall back to the bundled default vehicle type (with alert).
5. Log the resolved configuration at `Information` level.
6. Construct `RouteManager` (unchanged ordering — still before `SimulationManager`, per the startup-ordering fix already documented in the navigation doc §4).
7. Construct `SimulationManager(resolvedVehicleType, scenario)`.

`SimulationManager`'s constructor takes the resolved type and scenario, constructing `OwnVehicle(vehicleType, scenario)` directly — the only change to `SimulationManager`, since it already owned `OwnVehicle`'s construction. `OwnVehicle`'s previous parameterless constructor was removed entirely, not kept as a fallback overload — `OwnVehicle` should never exist without a resolved configuration behind it.

No ordering dependency on `Messenger` or `MfdViewModel` exists in any of this — `StartupAlerts` is a plain list, read later, whenever the UI is ready for it.

---

## 8. Resolved decisions

- **Turn rate is configuration-only.** No runtime setter; changing it means editing the vehicle type file. Confirmed nothing else in the codebase relied on the old settable behaviour.
- **Fallback is logged *and* alerted, and the fallback data itself is a real JSON file**, bundled as an Avalonia asset — consistent with how the application already guarantees other essential assets are always present regardless of the writable data folder's state.
- **Startup alerting uses a plain, pull-based list on `ApplicationFactory` (`StartupAlerts`), not a change to `Messenger`.** Generalises cleanly to configuration problems in domains other than vehicle configuration.

### 8a. Turn rate and acceleration calibration — deferred to a future vehicle dynamics model

Both `MaxTurnRateDegreesPerSecond` and the acceleration range are, for now, **uniform placeholder values across every vehicle type**, not individually-researched or calibrated figures. This was a deliberate decision, not an oversight: turning performance in degrees/second and acceleration limits are not published specifications for real vehicles the way top speed typically is, and assigning differentiated-but-unsourced numbers per vehicle type (e.g. "the tracked Stormer turns faster than the wheeled Piranha V") would create an appearance of researched precision that doesn't actually exist yet.

Calibrating these properly — reflecting genuine differences between tracked pivot-steer vehicles, wheeled Ackermann-steered platforms, and vehicle mass/power — is explicitly deferred until a higher-fidelity vehicle dynamics model is implemented. At that point, both fields become live inputs to real behavioural differences between vehicle types, rather than inert numbers carried for schema completeness; refining them now, ahead of that model, would be effort spent on precision the simulation can't yet act on.

---

## 9. Initial vehicle type library

Three real vehicle type files were produced using publicly available reference data:

| `VehicleTypeId` | `DisplayName` | `MaxSpeedKmh` | Source basis |
|---|---|---|---|
| `AlvisStormer` | Alvis Stormer | 80 | Published manufacturer/reference specifications (CVR(T)-family tracked vehicle) |
| `PiranhaV` | Piranha V | 100 | Published manufacturer/reference specifications (GDELS Mowag 8×8 wheeled AFV) |
| `Supacat` | Supacat HMT 600 | 120 | Manufacturer specification sheet |

Only `MaxSpeedKmh` is sourced from public data for each. `MinSpeedKmh` (`0`), `MaxTurnRateDegreesPerSecond`, and both acceleration fields are the uniform placeholder values discussed in §8a, not vehicle-specific research.

One naming note worth recording: the vehicle type library uses `Supacat` (the underlying platform) rather than `Wolfram` (a 2026 British Ministry of Defence project pairing a Brimstone missile launcher with this same Supacat HMT 600 chassis for Ukraine). Wolfram has no independently published mobility spec of its own — its performance is entirely inherited from the Supacat platform — so modelling the platform directly, rather than a specific weapons fit on top of it, was judged the more accurate and more broadly reusable choice for the vehicle type library.

---

## 10. Constraints carried / explicitly deferred

Carried forward:
- No new persistence framework — reused the existing folder-of-JSON-files pattern, narrowed to read-only where that's all that's needed.
- No speculative abstraction ahead of a concrete requirement.
- Configuration remains hand-edited files.
- `Messenger` remains unchanged — startup-specific handling lives in `ApplicationFactory` instead.

Explicitly deferred, not built now:
- **Multiple simultaneous vehicles.** One vehicle type for one `OwnVehicle` instance per run.
- **Launcher/missile/weapon configuration.** No weapon or launcher model exists anywhere in the codebase yet.
- **In-app configuration editing UI.** Vehicle type and scenario files are edited directly.
- **Acceleration modelling, and turn rate/acceleration calibration** (§8a) — both deferred to a future higher-fidelity vehicle dynamics model.
- **A generalised, multi-domain "application configuration" system.** `StartupAlerts` is deliberately just a flat list, not a categorised diagnostics framework.

---

## 11. Implementation history

1. ~~Add `VehicleType`/`ScenarioConfiguration` models, `IVehicleTypePersistence`/`IScenarioPersistence`, and their file-backed implementations~~ — done, verified in isolation via temporary logging in `MainWindow` before any `ApplicationFactory`/`OwnVehicle` wiring existed.
2. ~~Add bundled default vehicle type/scenario assets and `DefaultConfigurationAssets`~~ — done, including confirming the deliberate hard-failure behaviour for a missing bundled asset.
3. ~~Add `ApplicationFactory.StartupAlerts`/`RecordStartupAlert`, wire `MfdViewModel` to replay on subscribe~~ — done.
4. ~~Add fallback/resolution logic to `ApplicationFactory.Initialize()`~~ — done. Surfaced and fixed two real bugs during testing: the missing heading-range validation in `ScenarioPersistence` (§5), and the spurious double-alert on scenario fallback (§5).
5. ~~Extend `OwnVehicle`'s construction to accept `VehicleType`/`ScenarioConfiguration`, remove the parameterless constructor, remove the public turn-rate setter, add the speed clamp~~ — done.
6. ~~Wire `ApplicationFactory.Initialize()` and `SimulationManager`'s constructor together~~ — done.
7. ~~Produce an initial vehicle type library from public reference data~~ — done (§9); acceleration/turn-rate calibration explicitly deferred (§8a).

All steps tested end-to-end: clean-path run with a real vehicle type and matching scenario behaves correctly and uses the configured values, not hardcoded ones; the speed clamp holds during active navigation against an out-of-range waypoint speed; every fallback combination (missing/empty type library, missing/invalid scenario, unresolvable vehicle type ID, and both failures together) produces the correct alert(s) and a usable default; and no code outside `OwnVehicle`'s constructor sets its turn rate.
