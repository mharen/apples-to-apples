# Apples to Apples Rate Scraper

Scrapes Ohio energy rates from PUCO's Apples to Apples comparison tool and outputs JSON.

## Configuration

Utilities are configured in [Console/appsettings.json](src/Console/appsettings.json). Each utility has:
- `Id`: Unique identifier used as the command-line argument
- `AnnualUsage`: Annual usage for cost calculations (kWh for electric, MCF for gas)
- `RateUrl`: PUCO Apples to Apples comparison URL

## Build

```bash
cd src
dotnet build
```

## Run

```bash
cd Console

# Electric rates (Ohio Edison)
dotnet run -- ohio-edison

# Gas rates (Enbridge)
dotnet run -- enbridge
```

## Tip

Get the cheapest rate using jq:

```bash
# From src directory
dotnet run -- ohio-edison | jq '.[0]'

# From Console directory
dotnet run -- enbridge | jq '.[0]'
```
