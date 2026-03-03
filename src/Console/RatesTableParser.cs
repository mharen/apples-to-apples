using System.Globalization;
using System.Text.RegularExpressions;

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

    public static IReadOnlyList<Rate> ParseRates(string html)
    {
        var (headers, rows) = ParseTable(html);
        var columnIndexes = GetRequiredColumnIndexes(headers);
        var rates = new List<Rate>(rows.Count);

        foreach (var row in rows)
        {
            if (row.Count == 0)
            {
                continue;
            }

            var supplier = ParseSupplier(row[columnIndexes["Supplier"]]);
            var pricePerUnit = ParsePricePerUnit(row[columnIndexes["$/"]].Text);
            var rateType = ParseRateType(row[columnIndexes["Rate Type"]].Text);
            var termLengthMonths = ParseTermLengthMonths(row[columnIndexes["Term. Length"]].Text);
            var earlyTerminationFee = ParseCurrency(row[columnIndexes["Early Term. Fee"]].Text);
            var monthlyFee = ParseCurrency(row[columnIndexes["Monthly Fee"]].Text);

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

    private static (List<string> Headers, List<List<HtmlCell>> Rows) ParseTable(string html)
    {
        var tableMatches = TableRegex().Matches(html);
        
        foreach (Match tableMatch in tableMatches)
        {
            var tableHtml = tableMatch.Value;
            var headers = ExtractHeaders(tableHtml);
            
            if (HasAllRequiredHeaders(headers))
            {
                var rows = ExtractRows(tableHtml);
                return (headers, rows);
            }
        }
        
        throw new InvalidOperationException("Rates table not found.");
    }

    private static List<string> ExtractHeaders(string tableHtml)
    {
        var headers = new List<string>();
        var theadMatch = TheadRegex().Match(tableHtml);
        
        if (theadMatch.Success)
        {
            var headerMatches = ThRegex().Matches(theadMatch.Value);
            foreach (Match match in headerMatches)
            {
                headers.Add(ExtractText(match.Value));
            }
        }
        
        return headers;
    }

    private static List<List<HtmlCell>> ExtractRows(string tableHtml)
    {
        var rows = new List<List<HtmlCell>>();
        var tbodyMatch = TbodyRegex().Match(tableHtml);
        
        if (!tbodyMatch.Success)
        {
            return rows;
        }
        
        var rowMatches = TrRegex().Matches(tbodyMatch.Value);
        foreach (Match rowMatch in rowMatches)
        {
            var cells = new List<HtmlCell>();
            var cellMatches = TdRegex().Matches(rowMatch.Value);
            
            foreach (Match cellMatch in cellMatches)
            {
                var cellHtml = cellMatch.Value;
                cells.Add(new HtmlCell(cellHtml, ExtractText(cellHtml)));
            }
            
            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }
        
        return rows;
    }

    private static string ExtractText(string html)
    {
        html = ScriptStyleRegex().Replace(html, string.Empty);
        html = TagRegex().Replace(html, " ");
        html = html.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'");
        html = WhitespaceRegex().Replace(html, " ").Trim();
        return html;
    }

    private static string ExtractElementByClass(string html, string className)
    {
        var pattern = $@"<[^>]*class\s*=\s*[""'][^""']*\b{Regex.Escape(className)}\b[^""']*[""'][^>]*>(.*?)</[^>]+>";
        var match = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static bool HasAllRequiredHeaders(List<string> headers)
    {
        var normalizedHeaders = headers
            .Select(h => h.Replace(".", string.Empty, StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        return RequiredHeaders.All(required =>
        {
            var normalized = required.Replace(".", string.Empty, StringComparison.Ordinal);
            return normalizedHeaders.Any(h => h.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static Dictionary<string, int> GetRequiredColumnIndexes(List<string> headers)
    {
        var normalizedHeaders = headers.Select(h => NormalizeHeader(h)).ToArray();
        var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var required in RequiredHeaders)
        {
            var expected = NormalizeHeader(required);
            var index = Array.FindIndex(normalizedHeaders, h => h.Contains(expected, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
                throw new InvalidOperationException($"Required column '{required}' was not found.");

            columnIndexes[required] = index;
        }

        return columnIndexes;
    }

    private static string ParseSupplier(HtmlCell supplierCell)
    {
        var retailTitleHtml = ExtractElementByClass(supplierCell.Html, "retail-title");
        
        if (!string.IsNullOrEmpty(retailTitleHtml))
        {
            var textMatch = FirstTextNodeRegex().Match(retailTitleHtml);
            if (textMatch.Success)
            {
                var text = CleanWhitespace(textMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return CleanWhitespace(supplierCell.Text);
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

    [GeneratedRegex(@"^([^<]+)")]
    private static partial Regex FirstTextNodeRegex();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex TermMonthsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

internal readonly record struct HtmlCell(string Html, string Text);
