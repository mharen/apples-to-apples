using System.Globalization;
using System.Text.RegularExpressions;

namespace ApplesToApples.ConsoleApp;

public sealed partial class RatesTableParser
{
    // Short patterns to identify each required column; order matters for ambiguous headers
    // (e.g. "Term" matches "Term. Length" before "Early Term. Fee" since FindIndex takes the first)
    private static readonly string[] RequiredColumns = ["Supplier", "$/", "Rate Type", "Term", "Early", "Monthly"];

    public static IReadOnlyList<Rate> ParseRates(string html)
    {
        // Strip script/style blocks first so JS-embedded HTML strings don't fool the table finder
        html = ScriptStyleRegex().Replace(html, string.Empty);

        var tableHtml = TableRegex().Matches(html)
            .Cast<Match>()
            .Select(m => m.Value)
            .FirstOrDefault(t => RequiredColumns.All(p =>
                ExtractHeaders(t).Any(h => h.Contains(p, StringComparison.OrdinalIgnoreCase))))
            ?? throw new InvalidOperationException("Rates table not found.");

        var headers = ExtractHeaders(tableHtml);
        var col = (string pattern) =>
            Array.FindIndex(headers, h => h.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        return ExtractRows(tableHtml)
            .Where(row => row.Count > 0)
            .Select(row => new Rate(
                Supplier: ParseSupplier(row[col("Supplier")]),
                PricePerUnit: ParseDecimal(ExtractText(row[col("$/")])),
                RateType: ParseRateType(ExtractText(row[col("Rate Type")])),
                TermLengthMonths: ParseTermLength(ExtractText(row[col("Term")])),
                EarlyTerminationFee: ParseDecimal(ExtractText(row[col("Early")])),
                MonthlyFee: ParseDecimal(ExtractText(row[col("Monthly")]))))
            .ToList();
    }

    // Returns raw cell HTML strings (not yet text-extracted) so parsers can choose their approach
    private static string[] ExtractHeaders(string tableHtml) =>
        ThRegex().Matches(TheadRegex().Match(tableHtml).Value)
            .Select(m => ExtractText(m.Value))
            .ToArray();

    private static List<List<string>> ExtractRows(string tableHtml) =>
        TrRegex().Matches(TbodyRegex().Match(tableHtml).Value)
            .Select(tr => TdRegex().Matches(tr.Value).Select(td => td.Value).ToList())
            .Where(cells => cells.Count > 0)
            .ToList();

    private static string ExtractText(string html)
    {
        html = ScriptStyleRegex().Replace(html, string.Empty);
        html = TagRegex().Replace(html, " ");
        html = html.Replace("&nbsp;", " ").Replace("&amp;", "&")
                   .Replace("&lt;", "<").Replace("&gt;", ">")
                   .Replace("&quot;", "\"").Replace("&#39;", "'");
        return WhitespaceRegex().Replace(html, " ").Trim();
    }

    // Gets the first non-empty text node — picks supplier name over trailing address/sub-content
    internal static string ParseSupplier(string cellHtml)
    {
        var text = FirstTextNodeRegex().Matches(cellHtml)
            .Select(m => m.Groups[1].Value.Trim())
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return text ?? ExtractText(cellHtml);
    }

    private static decimal ParseDecimal(string text)
    {
        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        if (!decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Could not parse decimal from '{text}'.");
        return value;
    }

    private static RateType ParseRateType(string text)
    {
        if (text.Contains("fixed", StringComparison.OrdinalIgnoreCase)) return RateType.Fixed;
        if (text.Contains("variable", StringComparison.OrdinalIgnoreCase)) return RateType.Variable;
        throw new FormatException($"Unsupported rate type '{text}'.");
    }

    private static int ParseTermLength(string text)
    {
        var match = DigitsRegex().Match(text);
        if (!match.Success) throw new FormatException($"Could not parse term length from '{text}'.");
        return int.Parse(match.Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"<table[^>]*>.*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TableRegex();

    [GeneratedRegex(@"<thead[^>]*>.*?</thead>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TheadRegex();

    [GeneratedRegex(@"<tbody[^>]*>.*?</tbody>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TbodyRegex();

    [GeneratedRegex(@"<tr[^>]*>.*?</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TrRegex();

    [GeneratedRegex(@"<th[^>]*>.*?</th>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThRegex();

    [GeneratedRegex(@"<td[^>]*>.*?</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TdRegex();

    [GeneratedRegex(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@">([^<>]+)<", RegexOptions.Singleline)]
    private static partial Regex FirstTextNodeRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
