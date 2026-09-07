# GBMS — Vehicle Display Page

**Status:** Proposed design — fully specified, ready for implementation.
**Scope:** A new MFD functional area presenting a set of vehicle instrumentation displays — GPS position, an analogue/digital compass, speed, odometer, a circular analogue tachometer, a configured fuel gauge with low-fuel and out-of-fuel alerting, an engine start/stop control, and a local metric/imperial display toggle — matching the look and feel of a modern GVA in-vehicle display. Deliberately **not** a driving simulation: several displayed values are simplified or cosmetic approximations rather than physically modelled quantities.

**Depends on:** `GBMS_Route_Assignment_Navigation_Design.md` (`OwnVehicleState`, `OwnVehicle.Update`), `GBMS_Vehicle_Configuration_Design.md` (`OwnVehicle` construction, `VehicleType`, `ScenarioConfiguration`, `Messenger`/alert bar mechanism).

---

## 1. Purpose and design philosophy

GBMS is not a driving simulation. This page's job is to *look and behave* like a real GVA in-vehicle display, not to model vehicle systems with engineering fidelity. That deliberately licenses simplifications that would be inappropriate if accuracy were the goal:

- **RPM** is a cosmetic function of speed, not a real engine/powertrain model.
- **Fuel consumption** is a constant rate per km, not sensitive to load, terrain, or engine efficiency — but tank capacity and consumption rate are genuinely configured per vehicle type, since fuel behaviour differing between vehicles is an operationally meaningful comparison, unlike RPM.
- **GPS position** is displayed as raw truth data — `OwnVehicle.Position` shown directly, with no simulated GPS accuracy, drift, or signal loss.
- **Display units are a presentation choice only** — see §5. Underlying values remain metric everywhere else in GBMS (routes, vehicle configuration files, `OwnVehicle`'s own internal state); this page's imperial/metric toggle affects nothing beyond how numbers are formatted on this one screen.

Consistent with this project's MVP/KISS discipline throughout: no new subsystem (no engine or powertrain model) is introduced to support this page. Every new piece of state added is the minimum needed to make its corresponding display item behave plausibly.

---

## 2. Field list

### Already available — no new state

From `OwnVehicleState`:
- **GPS Position** (`Position.Latitude`/`Longitude`) — displayed as-is; see §5.
- **Compass Bearing** (`Position.Heading`) — drives both the analogue and digital compass presentation; see §5.
- **Speed** (current, converted to the selected display unit — see §5).

### New configuration fields

**`VehicleType`** gains two fuel-related fields, alongside the existing speed/turn-rate/acceleration ones:

```csharp
public double FuelTankCapacityLitres { get; set; }
public double FuelConsumptionRateLitresPerKm { get; set; }
```

**`ScenarioConfiguration`** gains the starting fuel state for the run:

```csharp
public double StartingFuelPercent { get; set; }
```

This mirrors the split already established for speed/turn-rate: intrinsic vehicle capability lives on `VehicleType`; what's true for *this particular run* lives on `ScenarioConfiguration`.

Both fields need validation added alongside the existing checks in `VehicleTypePersistence`/`ScenarioPersistence`: `FuelTankCapacityLitres`/`FuelConsumptionRateLitresPerKm` finite and positive; `StartingFuelPercent` finite and within `[0, 100]`. All three existing vehicle type files, the bundled default vehicle type asset, and the bundled default scenario asset will need these new fields added before this feature can run against them.

### New state on `OwnVehicle`

```csharp
private readonly double _fuelTankCapacityLitres;
private readonly double _fuelConsumptionRateLitresPerKm;

private bool _lowFuelAlertRaised;
private bool _outOfFuelAlertRaised;

public double FuelLevelLitres { get; private set; }

public double FuelLevelPercent =>
    _fuelTankCapacityLitres > 0.0
        ? FuelLevelLitres / _fuelTankCapacityLitres * 100.0
        : 0.0;

public bool IsEngineRunning { get; private set; } = true;
public double DistanceTravelledKm { get; private set; }
```

`FuelLevelPercent` is deliberately computed, not stored — a single source of truth (`FuelLevelLitres`) avoids the two values ever disagreeing. `FuelLevelLitres` is set at construction from `scenario.StartingFuelPercent / 100.0 * vehicleType.FuelTankCapacityLitres`.

### Derived, computed only — no domain state

- **RPM** — `IdleRpm + (Speed / VehicleType.MaxSpeedKmh) × (RedlineRpm − IdleRpm)`, or `0` if the engine is off. **Decided:** `IdleRpm = 700`, `RedlineRpm = 3500` — plausible-looking figures for a diesel-powered military vehicle, chosen purely for display authenticity with no real-world grounding required, consistent with this field's cosmetic-only purpose (§1).

---

## 3. Engine, fuel, and alert behaviour

`OwnVehicle.Update` gains a leading engine check, ahead of the existing route-assignment logic:

```csharp
public void Update(TimeSpan simulationStep)
{
    if (simulationStep <= TimeSpan.Zero)
        return;

    if (!IsEngineRunning)
    {
        Speed = 0.0;
        return;
    }

    // existing AssignedRoute / hold / UpdateNavigating logic, unchanged
}
```

Turning the engine off does **not** clear `AssignedRoute` — turning it back on resumes navigation from wherever it left off.

### Fuel depletion, inside `UpdateNavigating`

Alongside the existing distance/speed calculation, once `travelDistanceMetres` (or the final snapped distance on arrival) is known for the tick:

```csharp
double distanceThisTickKm = travelDistanceMetres / 1000.0;

DistanceTravelledKm += distanceThisTickKm;

double fuelUsedLitres = distanceThisTickKm * _fuelConsumptionRateLitresPerKm;

FuelLevelLitres = Math.Max(0.0, FuelLevelLitres - fuelUsedLitres);

CheckFuelAlerts();
```

Because this only ever runs while actually navigating, an engine-off vehicle already consumes zero fuel — no separate check needed at the depletion site itself.

### Alerting — two distinct, one-shot conditions

```csharp
private void CheckFuelAlerts()
{
    if (!_lowFuelAlertRaised && FuelLevelPercent < 10.0)
    {
        _lowFuelAlertRaised = true;
        RaiseFuelAlert("Fuel low - below 10%.");
    }

    if (!_outOfFuelAlertRaised && FuelLevelLitres <= 0.0)
    {
        _outOfFuelAlertRaised = true;
        IsEngineRunning = false;
        RaiseFuelAlert("Out of fuel - engine stopped.");
    }
}
```

Both flags are one-way latches, deliberately never reset — fuel only ever decreases (no refuelling exists, §6), so once a threshold is crossed it can never be un-crossed within a run.

### Resolved: `OwnVehicle` → alert path — event-based

**Decided (recommended option, adopted by default — not separately contested, flag if this isn't what was intended):** `OwnVehicle` does not depend on `Messenger` directly. It raises a plain event instead, keeping its existing dependency isolation (only `Mapping`, no services) intact:

```csharp
public event Action<string>? FuelAlertRaised;

private void RaiseFuelAlert(string message)
{
    FuelAlertRaised?.Invoke(message);
}
```

`SimulationManager` subscribes and forwards, exactly mirroring how it already forwards `RouteManager.AssignedRouteChanged` to `OwnVehicle.AssignRoute`:

```csharp
// In SimulationManager's constructor, alongside the existing
// _routeManager.AssignedRouteChanged += OnAssignedRouteChanged;
OwnVehicle.FuelAlertRaised += OnFuelAlertRaised;

// New handler:
private void OnFuelAlertRaised(string message)
{
    ApplicationFactory.Messenger.Send(message, isAlert: true);
}
```

### Out-of-fuel re-entry is handled implicitly

If the operator manually restarts the engine with `FuelLevelLitres <= 0`, the next `UpdateNavigating` tick immediately re-depletes (already zero, no-op) and `CheckFuelAlerts` finds `_outOfFuelAlertRaised` already `true`, so it correctly stays stopped without re-alerting. No explicit block on the engine-start action itself is needed.

---

## 4. MFD wiring

**DRV** is already defined in `MfdFunctionalArea` with `IsSimulated = true`, currently backed by `MfdPlaceholderViewModel("Driver")` — the natural home for this page.

- A new `DriverViewModel`, implementing `IMfdInputReceiver`, replaces the DRV placeholder.
- A new `DriverView` (Avalonia `UserControl`) bound to `DriverViewModel`, following `MfdView`'s existing content-swapping pattern.

| Button | Driver page |
|---|---|
| L1 | Toggle engine start/stop, with a clear visual state indicator (§5) |
| R1 | Toggle metric/imperial display units (§5) |

Both are independent of what L1/R1 mean in the SA context — context is already fully determined by `CurrentFunctionalArea`, the same principle that already lets L4 mean different things across SA/Route Manager/Route Editing. R2–R6, L2–L6 have no anticipated use on this page in its first cut and remain no-ops, consistent with `MfdPlaceholderViewModel`'s existing behaviour.

---

## 5. Presentation

### Implementation technique — standard Avalonia only, no third-party controls

Confirmed: every display on this page is achievable with plain Avalonia, reusing patterns already established elsewhere in GBMS. Avalonia has no built-in gauge/speedometer/compass control, but none is needed:

- **Static artwork** (dial faces, tick marks, redline zone, compass rose) as SVG assets, loaded exactly the way `TacticalSymbolResolver` already loads tactical symbols — `avares://` + `AssetLoader`, displayed via an `Image` control. Same asset pipeline, same `AvaloniaResource` entry in `GBMS.csproj` already used for icons and symbols.
- **The moving part** (tachometer needle, compass card) as a separate layered element with a `RotateTransform` bound to a computed angle — the same rotation-by-transform technique `TacticalSymbolLayer` already uses for `SymbolRotation`, just expressed in plain Avalonia XAML/code-behind rather than a Mapsui style.
- **Digital readouts** are plain `TextBlock`s bound to the underlying values.
- **Engine/unit-mode indicators** reuse the existing colour-changing `Border` idiom already used for functional-area status (`MfdView.axaml`) and the alert bar's boolean-driven brush (`AlertForeground` in `MfdViewModel`).

No new package dependency is introduced. Third-party Avalonia gauge/instrument-cluster libraries exist in the community ecosystem but were deliberately not adopted — this project's dependency list stays as lean as it already is (Avalonia, `CommunityToolkit.Mvvm`, `Mapsui`, `Serilog`), and a cosmetic-authenticity feature isn't sufficient reason to widen it.

### Tachometer — circular analogue gauge with digital readout

A dial face with tick marks across a fixed arc (e.g. 0° to ~270°), a needle rotating to the position corresponding to the current RPM value (mapped linearly between `IdleRpm` and `RedlineRpm`), with the upper portion of the arc marked as a redline zone. A digital numeric readout sits alongside or beneath the dial.

### Compass — analogue card with digital readout

Standard vehicle/aircraft heading-indicator convention: a **rotating compass card**, with a **fixed pointer/lubber line at the top** — as distinct from a handheld magnetic compass, where the needle rotates and the card stays fixed. The card rotates opposite to heading change (so current heading always reads at the fixed top marker), bound to `Heading`. A digital numeric readout (e.g. `090°`) sits alongside the card.

### GPS position — raw truth data, no map, no simulated inaccuracy

A plain digital readout of `Position.Latitude`/`Longitude`. Explicitly the simulation's *true* position, presented as if it were a GPS fix, with no modelled GPS error, drift, or signal degradation — a deliberate omission, not an oversight (§1).

### Engine state indicator

A dedicated status indicator (e.g. "ENGINE RUNNING" / "ENGINE STOPPED"), colour-coded (green/red) and bound to `IsEngineRunning` — more explicit feedback than relying on RPM alone dropping to zero.

### Metric/imperial display toggle — presentation only

R1 toggles a `DisplayUnitSystem` value (`Metric`/`Imperial`) held as local `DriverViewModel` state — no domain significance, not persisted, consistent with this project's established rule to keep UI state local when it carries no domain meaning. **Confirmed scope: this affects formatting on this page only** — routes, vehicle configuration files, and every other part of GBMS remain metric-only.

| Field | Metric | Imperial |
|---|---|---|
| Speed | km/h | mph |
| Odometer | km | miles |
| Fuel volume | litres | **imperial (UK) gallons** — confirmed, not US gallons, given GBMS's UK/GVA context |

`FuelLevelLitres` is already tracked internally (§2), so displaying it as a literal volume figure needs no new state — only a conversion at render time. **Unaffected by this toggle:** GPS position, compass bearing, RPM, and fuel percentage. A small visible mode indicator (e.g. a `METRIC`/`IMPERIAL` badge near R1's icon) accompanies the toggle, same reasoning as the engine indicator.

---

## 6. Constraints carried / explicitly deferred

- **No engine/powertrain model.** RPM remains a cosmetic derived value indefinitely.
- **No sophisticated fuel consumption modelling.** Constant litres-per-km, uniform regardless of speed, load, or terrain.
- **No refuelling mechanism.** Fuel only decreases for the duration of a run.
- **No simulated GPS inaccuracy.** Position is shown as ground truth.
- **No persistence.** Odometer, fuel level, engine state, and the display unit preference all reset to their initial values each application run.
- **No propagation of the unit toggle beyond this page.**
- **No new third-party dependency.** All instrumentation is built from standard Avalonia primitives and existing GBMS asset-loading patterns.

---

## 7. Resolved decisions

- **`OwnVehicle` → alert path: event-based**, forwarded through `SimulationManager` (§3) — keeps `OwnVehicle` free of any service dependency beyond `Mapping`, consistent with its established isolation.
- **Fuel tank capacity / consumption rate per vehicle type: research first.** Real figures should be sourced for `AlvisStormer`, `PiranhaV`, and `Supacat` where publicly available, using the same sourcing-honesty approach applied to `MaxSpeedKmh`. **Where no reliable public figure exists, fall back to a uniform placeholder** across the affected vehicle(s), consistent with how turn rate and acceleration were already handled. This research has not yet been performed — it's a prerequisite for implementation step 1 (§8), not yet completed as part of this design pass.
- **Imperial/metric toggle scope: presentation-only, this page only** — confirmed.
- **Fuel volume unit: imperial (UK) gallons** — confirmed.
- **No third-party UI dependency needed** — confirmed; standard Avalonia, reusing existing asset/rotation patterns (§5).

---

## 8. Suggested implementation order

1. **Research fuel tank capacity and consumption rate for each vehicle type** (§7); fall back to uniform placeholders for any vehicle without a reliable public figure. Add `FuelTankCapacityLitres`/`FuelConsumptionRateLitresPerKm` to `VehicleType`, `StartingFuelPercent` to `ScenarioConfiguration`, and corresponding validation. Update all three real vehicle type files, the bundled default vehicle type asset, and the bundled default scenario asset. Verify loading in isolation.
2. Add `IsEngineRunning`, `DistanceTravelledKm`, `FuelLevelLitres`/`FuelLevelPercent`, and the `FuelAlertRaised` event to `OwnVehicle`; wire the engine-off check as the first branch in `Update`; add fuel depletion and `CheckFuelAlerts` inside `UpdateNavigating`. Wire `SimulationManager`'s forwarding handler (§3). Verify via temporary logging: odometer/fuel change only while navigating; both alerts fire exactly once at the correct thresholds and appear on the alarm bar; a manual restart attempt with an empty tank correctly re-stops without re-alerting.
3. Extend `OwnVehicleState` (or a richer snapshot type) to carry the new fields to the UI.
4. Add `DriverViewModel`/`DriverView`, replacing the DRV placeholder; wire L1 (engine toggle + indicator) and R1 (unit toggle + indicator); add the compass and tachometer controls per §5's implementation technique; add the remaining read-only fields, applying the selected display unit conversion at render time only.
5. Manually test end-to-end: assign a route with a deliberately high consumption rate or low starting fuel percentage; confirm both alerts appear at the correct points and the vehicle correctly stops at empty; confirm `AssignedRoute` is retained and navigation resumes correctly across an engine off/on cycle; confirm the unit toggle changes displayed values on this page only, with routes and vehicle configuration elsewhere in the app unaffected.
