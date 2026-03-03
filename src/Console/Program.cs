using System.Text.Json;
using ApplesToApples.ConsoleApp;

Utility electricUtility = new Utility(
    "Ohio Edison",
    "https://www.energychoice.ohio.gov/ApplesToApplesComparision.aspx?Category=Electric&TerritoryId=7&RateCode=1",
    AnnualCostCalculator: (rate) =>
    {
        const int annualKwhUsage = 24000;
        const decimal monthsPerYear = 12m;
        return rate.PricePerUnit * annualKwhUsage
            + rate.MonthlyFee * monthsPerYear;
    }
);

Utility gasUtility = new Utility(
    "Enbridge",
    "https://www.energychoice.ohio.gov/ApplesToApplesComparision.aspx?Category=NaturalGas&TerritoryId=1&RateCode=1",
    AnnualCostCalculator: (rate) =>
    {
        const int annualMcfUsage = 100;
        const decimal monthsPerYear = 12m;
        return rate.PricePerUnit * annualMcfUsage
            + rate.MonthlyFee * monthsPerYear;
    }
);
const string source = "puco_apples_to_apples";

// parse out --gas or --electric flags
bool isGas = args.Any(a => string.Equals(a, "--gas", StringComparison.OrdinalIgnoreCase));
bool isElectric = args.Any(a => string.Equals(a, "--electric", StringComparison.OrdinalIgnoreCase));

// fail if both
if (isGas && isElectric || (!isGas && !isElectric))
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        timestamp = DateTimeOffset.UtcNow,
        source = "puco_apples_to_apples",
        status = "error",
        message = "Must set either --gas or --electric."
    }));
    Environment.ExitCode = 1;
    return;
}

var utility = isGas ? gasUtility : electricUtility;

try
{
    var cache = new HtmlDocumentCache(cacheTtl: TimeSpan.FromHours(1));
    var document = await cache.LoadDocumentAsync(utility.ApplesToApplesUrl);

    var rates = RatesTableParser.ParseRates(document);

    var ratesWithCost = rates
        .Where(r => r.RateType == RateType.Fixed
            && r.TermLengthMonths >= 12
            && r.PricePerUnit > 0)
        .Select(r => new RateWithCost(r, utility.AnnualCostCalculator(r)))
        .OrderBy(r => r.AnnualCost);

    var statesPayload = ratesWithCost.Select(ToPayload);
    var statesJson = JsonSerializer.Serialize(statesPayload);
    Console.WriteLine(statesJson);
    Environment.ExitCode = 0;
}
catch (Exception ex)
{
    var errorPayload = new
    {
        timestamp = DateTimeOffset.UtcNow,
        source = source,
        status = "error",
        message = ex.Message
    };

    Console.WriteLine(JsonSerializer.Serialize(errorPayload));
    Environment.ExitCode = 1;
}

static object ToPayload(RateWithCost r)
{
    var rate = r.Rate;
    var cost = r.AnnualCost;

    var statePayload = new
    {
        timestamp = DateTimeOffset.UtcNow,
        source,
        status = "ok",
        supplier = rate.Supplier,
        annual_cost = cost,
        price_per_unit = rate.PricePerUnit,
        term_months = rate.TermLengthMonths,
        monthly_fee = rate.MonthlyFee,
        etf = rate.EarlyTerminationFee,
    };
    return statePayload;
}

public record Utility(string Name, string ApplesToApplesUrl, Func<Rate, decimal> AnnualCostCalculator);
