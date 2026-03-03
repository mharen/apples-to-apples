# Apples to Apples Rate Scraper

Scrapes Ohio energy rates from PUCO's Apples to Apples comparison tool and outputs JSON.

## Build

```bash
cd src
dotnet build
```

## Run

```bash
# Electric rates
dotnet run --project Console/Console.csproj -- --electric

# Gas rates
dotnet run --project Console/Console.csproj -- --gas
```

## Test

```bash
dotnet test
```

## Tip

Get the cheapest rate using jq:

```bash
dotnet run --project Console/Console.csproj -- --electric | jq '.[0]'
```
