using System.Text.Json;
using ApplesToApples.ConsoleApp;
using Microsoft.Extensions.Configuration;

const string source = "puco_apples_to_apples";

// Load configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var utilities = configuration.GetSection("Utilities").Get<List<Utility>>();
if (utilities == null || utilities.Count == 0)
{
    ReturnError("No utilities configured in appsettings.json");
    return;
}

var utilitiesById = utilities.ToDictionary(u => u.Id, StringComparer.OrdinalIgnoreCase);


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
    var cache = new HtmlDocumentCache(cacheTtl: TimeSpan.FromHours(1));
    var document = await cache.LoadDocumentAsync(utility.RateUrl);

    var rates = RatesTableParser.ParseRates(document);

    var ratesWithCost = rates
        .Where(r => r.RateType == RateType.Fixed
            && r.TermLengthMonths >= 12
            && r.PricePerUnit > 0)
        .Select(r => new RateWithCost(r, CalculateAnnualCost(r, utility.AnnualUsage)))
        .OrderBy(r => r.AnnualCost);

    var statesPayload = ratesWithCost.Select(ToPayload);
    var statesJson = JsonSerializer.Serialize(statesPayload);
    Console.WriteLine(statesJson);
    Environment.ExitCode = 0;
}
catch (Exception ex)
{
    ReturnError(ex.Message);
}

static void ReturnError(string message)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        message,
        source,
        status = "error",
        timestamp = DateTimeOffset.UtcNow,
    }));
    
    Environment.ExitCode = 1;
}

static decimal CalculateAnnualCost(Rate rate, decimal annualUsage)
{
    const decimal monthsPerYear = 12m;
    return rate.PricePerUnit * annualUsage
        + rate.MonthlyFee * monthsPerYear;
}

static object ToPayload(RateWithCost r)
{
    var rate = r.Rate;
    var cost = r.AnnualCost;

    var statePayload = new
    {
        annual_cost = cost,
        etf = rate.EarlyTerminationFee,
        monthly_fee = rate.MonthlyFee,
        price_per_unit = rate.PricePerUnit,
        source,
        status = "ok",
        supplier = rate.Supplier,
        term_months = rate.TermLengthMonths,
        timestamp = DateTimeOffset.UtcNow,
    };
    return statePayload;
}

public record Utility(string Id, decimal AnnualUsage, string RateUrl);
