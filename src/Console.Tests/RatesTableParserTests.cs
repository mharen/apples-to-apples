using ApplesToApples.ConsoleApp;

namespace Console.Tests;

public sealed class RatesTableParserTests
{
    [Fact]
    public void ParseRates_ParsesTypedFields_FromTable()
    {
        const string html = """
            <html>
              <body>
                <table>
                  <thead>
                    <tr>
                      <th>Click<br/>to<br/>Compare</th>
                      <th>Supplier</th>
                      <th>$/kWh</th>
                      <th>Rate <br/> Type</th>
                      <th>Term. <br/> Length</th>
                      <th>Early <br/> Term. <br/> Fee</th>
                      <th>Monthly <br/> Fee</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td></td>
                      <td><span class='retail-title'>Alpha Energy<p>Address</p></span></td>
                      <td>0.1076</td>
                      <td><img src='x.png'/>Fixed</td>
                      <td>6 mo.</td>
                      <td>$10 <a href='#'>details</a></td>
                      <td>$14.95</td>
                    </tr>
                    <tr>
                      <td></td>
                      <td><span class='retail-title'>Beta Power<p>Address</p></span></td>
                      <td>0.0912</td>
                      <td>Variable</td>
                      <td>12 mo.</td>
                      <td>$0</td>
                      <td>$0</td>
                    </tr>
                  </tbody>
                </table>
              </body>
            </html>
            """;

        var rates = RatesTableParser.ParseRates(html);

        Assert.Equal(2, rates.Count);

        var rate1 = rates[0];
        Assert.Equal("Alpha Energy", rate1.Supplier);
        Assert.Equal(0.1076m, rate1.PricePerUnit);
        Assert.Equal(RateType.Fixed, rate1.RateType);
        Assert.Equal(6, rate1.TermLengthMonths);
        Assert.Equal(10m, rate1.EarlyTerminationFee);
        Assert.Equal(14.95m, rate1.MonthlyFee);


        var rate2 = rates[1];
        Assert.Equal("Beta Power", rate2.Supplier);
        Assert.Equal(0.0912m, rate2.PricePerUnit);
        Assert.Equal(RateType.Variable, rate2.RateType);
        Assert.Equal(12, rate2.TermLengthMonths);
        Assert.Equal(0m, rate2.EarlyTerminationFee);
        Assert.Equal(0m, rate2.MonthlyFee);
    }

    [Theory]
    [InlineData("Data/Raw-Electric.html")]
    [InlineData("Data/Raw-Gas.html")]
    public async Task ParseRates_ParsesRealRatesTable_FromSavedHtml(string fileName)
    {
        var rawHtmlPath = Path.Combine(AppContext.BaseDirectory, fileName);
        var html = await File.ReadAllTextAsync(rawHtmlPath);

        var rates = RatesTableParser.ParseRates(html);

        Assert.NotEmpty(rates);
        Assert.All(rates, rate =>
        {
            Assert.False(string.IsNullOrWhiteSpace(rate.Supplier));
            Assert.True(rate.TermLengthMonths >= 0);
            Assert.True(rate.PricePerUnit >= 0);
            Assert.True(rate.EarlyTerminationFee >= 0);
            Assert.True(rate.MonthlyFee >= 0);
        });
    }
}
