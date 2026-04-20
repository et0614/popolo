# Popolo Samples

A single-executable sample runner for **Popolo.Core**, **Popolo.IO**, and
**Popolo.Webpro**. Each sample is selected by name on the command line.

## Usage

```sh
# List all available demos
dotnet run --project samples/Popolo.Samples -- list

# Run a specific demo
dotnet run --project samples/Popolo.Samples -- <demo-name> [demo args...]
```

## Available demos

### Core

Short, self-contained demos that print a summary to the console — no external
input files required.

| Name | Description |
|------|-------------|
| `physics-moist-air` | Psychrometric properties (h, Twb, RH, v) at a few T/w pairs; plus altitude-vs-pressure table. |
| `physics-steam` | Saturated-steam table (P_sat, h_f, h_g, h_fg, v_g) for several temperatures, plus inverse T_sat lookup. |
| `climate-sun` | Solar altitude/azimuth, sunrise, and sunset at Tokyo for summer- and winter-solstice days. |
| `climate-incline` | Tilted-surface irradiance (direct, diffuse, total) for several orientations under a prescribed solar state. |
| `numerics-ode` | Fixed-step RK4 integration of Newton's law of cooling, compared against the analytical solution. |
| `numerics-regression` | Simple linear fit and a two-feature least-squares fit on synthetic noisy data. |
| `building-wall` | Multi-layer wall response to a 24-hour sinusoidal sol-air temperature. |
| `hvac-chiller` | Centrifugal chiller (constant-speed vs. inverter) COP at several part-load / ambient points. |
| `comfort-pmv` | Fanger PMV / PPD at a few typical office indoor conditions; plus inverse lookup for thermal neutrality. |
| `comfort-tanabe` | Tanabe 65-node body model skin/core response to a warm→cool environmental step. |
| `vrf-nedo-test` | VRF system annual energy test against the NEDO catalogue (Daikin VRV-X). *Longer; consider running once.* |

### IO

Demos that load or save file data. See `SampleData/` for bundled input files.

| Name | Description |
|------|-------------|
| `weather-to-csv` | Read an EPW / HASP / TMY1 weather file and convert to Popolo CSV. Prints a summary of the input. |

### Webpro

Japan-local demos using the WEBPRO compliance format.

| Name | Description |
|------|-------------|
| `webpro-annual` | Annual thermal load simulation from a WEBPRO JSON. Writes per-zone dry-bulb temperature, humidity ratio, and sensible/latent loads to CSV. |

#### Examples

```sh
# Core (no arguments, prints directly to stdout)
dotnet run --project samples/Popolo.Samples -- physics-moist-air
dotnet run --project samples/Popolo.Samples -- climate-sun

# IO
dotnet run --project samples/Popolo.Samples -- weather-to-csv \
  samples/Popolo.Samples/SampleData/tokyo.epw

# Webpro
dotnet run --project samples/Popolo.Samples -- webpro-annual \
  tests/Popolo.Webpro.Tests/TestData/builelib_input.json \
  out.csv
```

## Adding a new demo

1. Create a class implementing `IDemo` under `Demos/<category>/<name>Demo.cs`.
2. Register the instance in the `Demos` array in `Program.cs`.
3. Add a row to the table above in this README.

Demo `Name` values should be short kebab-case identifiers (`webpro-annual`,
`core-conduction`, `io-roundtrip`). Categories group related demos in the
listing.

## Why samples, not unit tests

The demos here perform non-deterministic or long-running work (stochastic
weather generation, 8760-hour simulations, CSV file output) and have no
pass/fail verdict. They exist as reference implementations showing how the
Popolo libraries fit together end-to-end, and as a starting point users can
copy and adapt.

## Requirements

- .NET 10 SDK
