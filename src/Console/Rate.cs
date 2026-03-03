namespace ApplesToApples.ConsoleApp;

public enum RateType
{
    Fixed,
    Variable
}

public sealed record Rate(
    string Supplier,
    decimal PricePerUnit,
    RateType RateType,
    int TermLengthMonths,
    decimal EarlyTerminationFee,
    decimal MonthlyFee);

public record RateWithCost(Rate Rate, decimal AnnualCost);
