using System.Text.Json;
using System.Text.Json.Serialization;
using ApplesToApples.ConsoleApp;

const string source = "puco_apples_to_apples";

// Load configuration using source-generated JSON
var configJson = await File.ReadAllTextAsync("appsettings.json");
var config = JsonSerializer.Deserialize(configJson, AppJsonContext.Default.AppConfig);

if (config?.Utilities == null || config.Utilities.Count == 0)
{
    ReturnError("No utilities configured in appsettings.json");
    return;
}

var utilitiesById = config.Utilities.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);

// Get utility name from args
if (args.Length == 0)
{
    ReturnError($"Must specify utility name. Available: {string.Join(", ", utilitiesById.Keys)}");
    return;
}

var utilityName = args[0];
if (!utilitiesById.TryGetValue(utilityName, out var utility))
{
    ReturnError($"Unknown utility '{utilityName}'. Available: {string.Join(", ", utilitiesById.Keys)}");
    return;
}

try
{
    var cacheDir = Environment.GetEnvironmentVariable("CACHE_DIR");
    var cache = new HtmlDocumentCache(cacheDirectory: cacheDir, cacheTtl: TimeSpan.FromHours(1));
    var html = await cache.LoadDocumentAsync(utility.RateUrl);

    var rates = RatesTableParser.ParseRates(html);

    var ratesWithCost = rates
        .Where(r => r.RateType == RateType.Fixed
            && r.TermLengthMonths >= 12
            && r.PricePerUnit > 0)
        .Select(r => new RateWithCost(r, CalculateAnnualCost(r, utility.AnnualUsage)))
        .OrderBy(r => r.AnnualCost);

    var payloads = ratesWithCost.Select(ToPayload).ToList();
    var json = JsonSerializer.Serialize(payloads, AppJsonContext.Default.ListRatePayload);
    Console.WriteLine(json);
    Environment.ExitCode = 0;
}
catch (Exception ex)
{
    ReturnError(ex.Message);
}

static void ReturnError(string message)
{
    var error = new ErrorResponse(message, source, "error", DateTimeOffset.UtcNow);
    var json = JsonSerializer.Serialize(error, AppJsonContext.Default.ErrorResponse);
    Console.WriteLine(json);

    Environment.ExitCode = 1;
}

static decimal CalculateAnnualCost(Rate rate, decimal annualUsage)
{
    const decimal monthsPerYear = 12m;
    return rate.PricePerUnit * annualUsage
        + rate.MonthlyFee * monthsPerYear;
}

static RatePayload ToPayload(RateWithCost r)
{
    var rate = r.Rate;
    var cost = r.AnnualCost;

    return new RatePayload(
        AnnualCost: cost,
        EarlyTerminationFee: rate.EarlyTerminationFee,
        MonthlyFee: rate.MonthlyFee,
        PricePerUnit: rate.PricePerUnit,
        Source: source,
        Status: "ok",
        Supplier: rate.Supplier,
        TermMonths: rate.TermLengthMonths,
        Timestamp: DateTimeOffset.UtcNow
    );
}

public record ErrorResponse(
    string Message,
    string Source,
    string Status,
    DateTimeOffset Timestamp
);

public record RatePayload(
    decimal AnnualCost,
    decimal EarlyTerminationFee,
    decimal MonthlyFee,
    decimal PricePerUnit,
    string Source,
    string Status,
    string Supplier,
    int TermMonths,
    DateTimeOffset Timestamp
);

[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(List<RatePayload>))]
[JsonSerializable(typeof(AppConfig))]
public partial class AppJsonContext : JsonSerializerContext { }

public record AppConfig(List<Utility> Utilities);

public record Utility(string Id, decimal AnnualUsage, string RateUrl);
