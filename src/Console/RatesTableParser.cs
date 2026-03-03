using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace ApplesToApples.ConsoleApp;

public sealed partial class RatesTableParser
{
    private static readonly string[] RequiredHeaders =
    [
        "Supplier",
        "$/",
        "Rate Type",
        "Term. Length",
        "Early Term. Fee",
        "Monthly Fee"
    ];

    public static IReadOnlyList<Rate> ParseRates(IDocument document)
    {
        var tableElement = document
            .QuerySelectorAll("table")
            .FirstOrDefault(IsRatesTable)
            ?? throw new InvalidOperationException("Rates table not found.");

        var columnIndexes = GetRequiredColumnIndexes(tableElement);
        var rows = tableElement.QuerySelectorAll("tbody tr");
        var rates = new List<Rate>(rows.Length);

        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length == 0)
            {
                continue;
            }

            var supplier = ParseSupplier(cells[columnIndexes["Supplier"]]);
            var pricePerUnit = ParsePricePerUnit(cells[columnIndexes["$/"]].TextContent);
            var rateType = ParseRateType(cells[columnIndexes["Rate Type"]].TextContent);
            var termLengthMonths = ParseTermLengthMonths(cells[columnIndexes["Term. Length"]].TextContent);
            var earlyTerminationFee = ParseCurrency(cells[columnIndexes["Early Term. Fee"]].TextContent);
            var monthlyFee = ParseCurrency(cells[columnIndexes["Monthly Fee"]].TextContent);

            rates.Add(new Rate(
                Supplier: supplier,
                PricePerUnit: pricePerUnit,
                RateType: rateType,
                TermLengthMonths: termLengthMonths,
                EarlyTerminationFee: earlyTerminationFee,
                MonthlyFee: monthlyFee));
        }

        return rates;
    }

    private static bool IsRatesTable(IElement tableElement)
    {
        var headers = tableElement.QuerySelectorAll("th").Select(th => NormalizeHeader(th.TextContent)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return RequiredHeaders.All(required => headers.Any(h => h.Contains(NormalizeHeader(required), StringComparison.OrdinalIgnoreCase)));
    }

    private static Dictionary<string, int> GetRequiredColumnIndexes(IElement tableElement)
    {
        var headers = tableElement.QuerySelectorAll("thead th").Select(th => NormalizeHeader(th.TextContent)).ToArray();
        var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var required in RequiredHeaders)
        {
            var expected = NormalizeHeader(required);
            var index = Array.FindIndex(headers, h => h.Contains(expected, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
                throw new InvalidOperationException($"Required column '{required}' was not found.");

            columnIndexes[required] = index;
        }

        return columnIndexes;
    }

    private static string ParseSupplier(IElement supplierCell)
    {
        var retailTitle = supplierCell.QuerySelector(".retail-title");
        if (retailTitle is null)
            return CleanWhitespace(supplierCell.TextContent);

        var textNode = retailTitle.ChildNodes.FirstOrDefault(n => n.NodeType == NodeType.Text)?.TextContent;
        if (!string.IsNullOrWhiteSpace(textNode))
            return CleanWhitespace(textNode);

        return CleanWhitespace(retailTitle.TextContent);
    }

    private static decimal ParsePricePerUnit(string text) => ParseInvariantDecimal(text);

    private static RateType ParseRateType(string text)
    {
        var value = CleanWhitespace(text);

        if (value.Contains("fixed", StringComparison.OrdinalIgnoreCase))
            return RateType.Fixed;

        if (value.Contains("variable", StringComparison.OrdinalIgnoreCase))
            return RateType.Variable;

        throw new FormatException($"Unsupported rate type value '{value}'.");
    }

    private static int ParseTermLengthMonths(string text)
    {
        var match = TermMonthsRegex().Match(text);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var months))
            throw new FormatException($"Could not parse term length from '{CleanWhitespace(text)}'.");

        return months;
    }

    private static decimal ParseCurrency(string text)
    {
        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        if (!decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Could not parse currency value from '{CleanWhitespace(text)}'.");

        return value;
    }

    private static decimal ParseInvariantDecimal(string text)
    {
        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        if (!decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Could not parse decimal value from '{CleanWhitespace(text)}'.");

        return value;
    }

    private static string NormalizeHeader(string text)
    {
        var cleaned = CleanWhitespace(text).Replace(".", string.Empty, StringComparison.Ordinal);
        return cleaned;
    }

    private static string CleanWhitespace(string text) => WhitespaceRegex().Replace(text, " ").Trim();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex TermMonthsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
