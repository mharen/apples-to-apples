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
  [InlineData("<td><span class='retail-title'>Alpha Energy<p>123 Main St</p></span></td>", "Alpha Energy")]
  [InlineData("<td><span class='retail-title'>  Trimmed Name  <p>Address</p></span></td>", "Trimmed Name")]
  [InlineData("<td>No Child Elements</td>", "No Child Elements")]
  [InlineData("<td><span>Public Power LLC P.O. Box 660823 Dallas,TX 75266-0823 (888) 354-4415<p>Company Url</p></span></td>", "Public Power LLC P.O. Box 660823 Dallas,TX 75266-0823 (888) 354-4415")]
  [InlineData("<td><span class='retail-title'>Santanna Energy Services<p>300 E Business Way, Suite 200<br/>Cincinnati,OH 45241</p><p>(866) 938-1881</p></span><p><a href='https://' target='_blank'>Sign Up</a></li></ul></td>", "Santanna Energy Services")]
  [InlineData("""
    <td >
      <span class='retail-title'>Public Power LLC<p>P.O. Box 660823<br/>Dallas,TX 75266-0823</p><p>(888) 354-4415</p></span><p><a href='https://www.publicpowercompany.com/?PromoCode=Rateboard&rfid=PUCO' target='_blank'>Company Url</a></p><p><a  href='javascript:return false;' onclick='showTextInDialog("Offer Details","With a fixed rate from Public Power, you&#39;ll get the peace of mind knowing your rate stays the same for 18 months. Special offer for new, online customers.");'>Offer Details</a></p><ul class='retail-desc'><li><a href='https://shopping.publicpowercompany.com/cust/?PID=ECA22A6F-9AAC-4617-BB5A-2BEE683624C5&UID=01E8CAF6-1AE8-4CF4-B1F3-ED7BB3044F8C&OID=17EE0331-7009-4AC4-B9E6-7D720F577489&RFID=PUCO&PUC=1&STATE=OH&SN=PP&CT=E&PromoCode=RATEBOARD' target='_blank'>Terms of Service</a></li><li><a class='red' href='https://shopping.publicpowercompany.com/cust/?PID=ECA22A6F-9AAC-4617-BB5A-2BEE683624C5&UID=01E8CAF6-1AE8-4CF4-B1F3-ED7BB3044F8C&OID=17EE0331-7009-4AC4-B9E6-7D720F577489&RFID=PUCO&PUC=1&STATE=OH&SN=PP&CT=E&PromoCode=RATEBOARD' target='_blank'>Sign Up</a></li></ul>
    </td>
    """, "Public Power LLC")]
  public void ParseSupplier_ExtractsFirstTextNode(string cellHtml, string expected)
  {
    var result = RatesTableParser.ParseSupplier(cellHtml);
    Assert.Equal(expected, result);
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
