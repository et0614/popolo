# Popolo

Popolo is an open-source C# library for academic simulation of building thermal environments and HVAC systems. It provides physics-based models for heat transfer, thermophysical properties, climate data, occupant comfort, and HVAC equipment — designed for researchers who value long-term stability, portability, and minimal external dependencies.

---

## Design Philosophy

- **No external runtime dependencies.** The core library ships as a single `Popolo.Core.dll` assembly. You can drop it into any .NET project and start simulating — no NuGet tree to manage, no version conflicts.
- **Physics-based, not black-box.** Every model is grounded in documented equations (ASHRAE, JIS, academic literature) and exposes its internal state through read-only interfaces so you can inspect intermediate quantities for research reproducibility.
- **Typed exceptions for diagnosable failures.** Numerical divergence, out-of-range inputs, and unsupported configurations are surfaced as distinct `Popolo*Exception` subclasses, so downstream tools can react to them programmatically instead of parsing error strings.
- **Separation of concerns.** IO, file formats (WEBPRO, weather file readers, JSON), and solvers are kept out of the core. The physics stays pure; serialization layers can be added on top without touching the models.

---

## Features

- **Numerical Computing**
  - Linear algebra (dense & sparse matrices, QR decomposition, LU solve)
  - Root finding (univariate / multivariate, Newton–Raphson)
  - Optimization (Nelder–Mead, golden-section, Levenberg–Marquardt)
  - ODE integration, Gauss–Legendre quadrature, cubic splines, FFT
  - Random number generation (Mersenne Twister, Normal / Log-normal / Gamma)
- **Thermodynamic Properties**
  - Liquid water and steam (IAPWS correlations)
  - Moist air (psychrometrics at arbitrary atmospheric pressure)
  - Refrigerants (R22, R134a, R410A, R407C, R32, etc.)
  - Lithium bromide aqueous solution
- **Climate**
  - Solar position and irradiance on arbitrary inclined surfaces
  - Sky radiation (Berdahl–Fromberg, Brunt, cloud-cover correction)
  - Stochastic weather generation from daily statistics (Watanabe method)
  - Ground temperature (Kusuda model)
- **Heat Exchange & Equipment**
  - Counterflow / parallel-flow / cross-flow heat exchange (ε-NTU, LMTD)
  - Tube-in-fin heat exchanger, evaporator, condenser (with frost modelling)
  - Air-to-air flat plate heat exchanger (sensible and enthalpy types)
  - Cooling tower (cross-flow / counter-flow)
  - Boiler (hot water, steam)
  - Compression refrigerator (simple / detailed centrifugal inverter chillers)
  - Absorption chiller (hot water driven, direct-fired, adsorption)
  - Air-source modular chillers, water heat pumps
- **Fluid & Network Systems**
  - Centrifugal pumps and fans (with inverter / VFD control)
  - Fluid circuit network solver (Hardy-Cross style with nodal pressures)
  - Parallel / series / controllable branches
  - Water and air piping (insulated) with heat loss calculation
- **Renewable & Thermal Storage**
  - Flat-plate solar thermal collector
  - Photovoltaic panel (simplified and detailed)
  - Stratified water tank (multi-node)
  - Ice-on-coil thermal storage
  - Simple ground heat exchanger
- **Building Envelope**
  - Multi-layer wall heat conduction (response factor method)
  - PCM (phase change material) wall layers
  - Horizontal air chambers (radiative + convective)
  - Buried pipes (radiant floor heating / cooling)
  - Windows with venetian blinds, sunshades, simple shading
- **System Simulation**
  - Multi-room thermal balance (MultiRooms, BuildingThermalModel)
  - Air handling unit with coils, humidifier, economizer
  - AHU / heat source sub-system orchestration
  - Variable Refrigerant Flow (VRF) with factory methods for
    Daikin VRV-X/VRV-A, Hitachi Set-Free SS, Toshiba MMY catalogues
- **Human-Centric Modelling**
  - PMV / PPD (Fanger model)
  - SET*, two-node body model (Gagge model)
  - 16-segment multi-node human body model (Tanabe)
  - Occupant thermal preference distribution (Takakusaki model)
  - Stochastic occupant behaviour (Langevin model, office tenant / worker)

---

## Requirements

- **.NET 10** or later
- No third-party NuGet packages

Popolo.Core has no runtime dependencies. Building and testing the repository requires only the .NET SDK.

---

## Installation

```powershell
dotnet add package Popolo.Core
```

Or download `Popolo.Core.dll` from the releases page and reference it directly.

---

## Quick Example

Psychrometric calculation at standard atmospheric pressure:

```csharp
using Popolo.Core.Physics;

var air = new MoistAir(
    dryBulbTemperature: 26.0,      // [°C]
    humidityRatio: 0.0105);        // [kg/kg(DA)]

Console.WriteLine($"Enthalpy         : {air.Enthalpy:F2} kJ/kg");
Console.WriteLine($"Wet-Bulb temp.   : {air.WetBulbTemperature:F2} °C");
Console.WriteLine($"Relative humidity: {air.RelativeHumidity:F1} %");
```

Solar position at Tokyo on the summer solstice:

```csharp
using Popolo.Core.Climate;

var sun = new Sun(Sun.City.Tokyo);
sun.Update(new DateTime(2026, 6, 21, 12, 0, 0));

Console.WriteLine($"Altitude : {sun.Altitude * 180 / Math.PI:F2}°");
Console.WriteLine($"Azimuth  : {sun.Azimuth  * 180 / Math.PI:F2}°");
```

A centrifugal chiller + cooling tower system under 60% load:

```csharp
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.SystemModel;
using Popolo.Core.Physics;

const double Cp = 4.186;                          // [kJ/(kg·K)]
const double chFlow = 500.0 / (12 - 7) / Cp;      // rated chilled water flow
const double cdFlow = 1670.0 / 60;                // rated condenser flow

var chiller = new SimpleCentrifugalChiller(
    ratedCapacity: 500.0 / 6.0, minPLR: 0.2,
    ratedChWSupplyTemp: 12, ratedChWReturnTemp: 7,
    ratedCWEnteringTemp: 37, ratedCWFlow: chFlow,
    hasInverter: false);

var tower = new CoolingTower(
    ratedCWEnteringTemp: 37, ratedCWLeavingTemp: 32, ratedWetBulbTemp: 27,
    ratedFlow: cdFlow, airFlow: CoolingTower.AirFlowDirection.CrossFlow,
    hasInverter: false);

var system = new HeatSourceSystemModel(
    new IHeatSourceSubSystem[] { /* ... assemble your subsystem ... */ });
system.OutdoorAir = new MoistAir(35, 0.0195);
system.ChilledWaterSupplyTemperatureSetpoint = 7.0;
system.TimeStep = 3600;
```

See `tests/Popolo.Core.Samples/` for runnable examples.

---

## Validation

The `tests/BESTEST/` project runs a subset of the ASHRAE Standard 140 / BESTEST validation suite. Results are written to per-case CSV files and compared against reference tool ranges. This lets you verify that Popolo's predictions sit within the envelope of established building simulation tools (EnergyPlus, DOE-2, ESP-r, etc.) for the same input conditions.

---

## Exception Hierarchy

Popolo raises typed exceptions that downstream code can catch selectively:

| Exception | When it is thrown |
|---|---|
| `PopoloArgumentException` | An argument violates a physical or numerical constraint (unsupported enum value, wrong array length, etc.) |
| `PopoloOutOfRangeException` | A value is outside a known physical/mathematical bound. Carries `Minimum` / `Maximum` properties. |
| `PopoloNumericalException` | An iterative solver failed to converge or hit a singular matrix. Carries `SolverName`. |
| `PopoloInvalidOperationException` | A method call is invalid for the current object state (e.g. missing required property). |
| `PopoloNotImplementedException` | An unimplemented code path was reached. |

All of these inherit from their corresponding BCL base classes (`ArgumentException`, `InvalidOperationException`, etc.), so existing broad `catch` blocks continue to work unchanged.

---

## Repository Layout

```
src/
  Popolo.Core/            # Main library (no external dependencies)
    Building/             # Zones, envelope, multi-room thermal balance
    Climate/              # Sun, sky, weather generation, ground
    Energy/               # PV panels
    Exceptions/           # Popolo* exception hierarchy
    Geometry/             # 3D primitives, view factors
    HVAC/                 # Equipment and system models
    Numerics/             # Linear algebra, solvers, random generators
    OccupantBehavior/     # Tenant / worker behavioural models
    Physics/              # Thermodynamic property calculators
    ThermalComfort/       # Fanger, Gagge, Tanabe, Takakusaki models
    Utilities/            # Miscellaneous helpers

tests/
  Popolo.Core.Tests/      # Unit tests (xUnit)
  Popolo.Core.Samples/    # Runnable usage examples
  BESTEST/                # ASHRAE 140 validation runner
```

---

## License

Popolo is distributed under the **GNU General Public License v3.0 or later** (GPL-3.0-or-later). See `LICENSE` for details.

---

## Citation

If you use Popolo in academic work, please cite the project repository:

```
Togashi, E. Popolo: A C# library for building thermal environment
and HVAC system simulation. https://github.com/et0614/popolo
```

---

## Links

- Project page: https://github.com/et0614/popolo
- Issue tracker: https://github.com/et0614/popolo/issues
