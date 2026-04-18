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

### Webpro

| Name | Description |
|------|-------------|
| `webpro-annual` | Annual thermal load simulation from a WEBPRO JSON. Writes per-zone dry-bulb temperature, humidity ratio, and sensible/latent loads to CSV. |

#### Example

```sh
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
