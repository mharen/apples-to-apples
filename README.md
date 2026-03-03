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

## Docker

Build the Docker image:

```bash
docker build -t apples-to-apples .
```

Run with Docker:

```bash
# Electric rates (Ohio Edison)
docker run --rm apples-to-apples ohio-edison

# Gas rates (Enbridge)
docker run --rm apples-to-apples enbridge

# Get cheapest rate using jq
docker run --rm apples-to-apples ohio-edison | jq '.[0]'
```

## Tip

Get the cheapest rate using jq:

```bash
# From src directory
dotnet run -- ohio-edison | jq '.[0]'

# From Console directory
dotnet run -- enbridge | jq '.[0]'
```

Example output:

```json
{
  "annualCost": 389.0000,
  "earlyTerminationFee": 100,
  "monthlyFee": 0,
  "pricePerUnit": 3.8900,
  "source": "puco_apples_to_apples",
  "status": "ok",
  "supplier": "Santanna Energy Services",
  "termMonths": 12,
  "timestamp": "2026-03-03T02:42:00.734664+00:00"
}
```
