using System.Text.Json;
using System.Globalization;
using OT.Assessment.Api.Endpoints;

namespace OT.Assessment.UnitTests;

public sealed class CasinoWagerRequestValidatorTests
{
    [Fact]
    public void AcceptsTesterPayloadAndPreservesJsonContract()
    {
        const string payload = """
            {
              "wagerId": "aa6700eb-1a06-483e-9739-d293dc7a9383",
              "theme": "adventure",
              "provider": "Ergonomic Soft Fish",
              "gameName": "Ergonomic Granite Cheese",
              "transactionId": "410b7161-3473-4d74-85c3-a533d050a9d3",
              "accountId": "5ac75fec-23e9-27d1-b660-179eee70003d",
              "Username": "Jay.Bernhard67",
              "amount": 38273.974454660885,
              "createdDateTime": "2024-05-04T02:25:05.9906387+02:00",
              "duration": 1827254
            }
            """;

        var request = JsonSerializer.Deserialize<CasinoWagerRequest>(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Empty(CasinoWagerRequestValidator.Validate(request));
        Assert.Equal("Jay.Bernhard67", request.Username);
        Assert.Equal(38273.974454660885m, request.Amount);
        Assert.Equal(TimeSpan.FromHours(2), request.CreatedDateTime.Offset);
    }

    [Fact]
    public void RejectsInvalidIdentifiersAmountsAndTextFields()
    {
        var request = new CasinoWagerRequest
        {
            WagerId = Guid.Empty,
            AccountId = Guid.Empty,
            Username = " ",
            Provider = new string('p', 201),
            GameName = "",
            Amount = 0m,
            CreatedDateTime = default
        };

        var errors = CasinoWagerRequestValidator.Validate(request);

        Assert.Contains("wagerId", errors.Keys);
        Assert.Contains("accountId", errors.Keys);
        Assert.Contains("Username", errors.Keys);
        Assert.Contains("provider", errors.Keys);
        Assert.Contains("gameName", errors.Keys);
        Assert.Contains("amount", errors.Keys);
        Assert.Contains("createdDateTime", errors.Keys);
    }

    [Theory]
    [InlineData("0.00000000000000001")]
    [InlineData("99999999999.99999999999999999")]
    public void AcceptsSupportedDecimalBoundaryAmounts(string amountText)
    {
        var request = new CasinoWagerRequest
        {
            WagerId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Username = "valid.player",
            Provider = "Provider",
            GameName = "Game",
            Amount = decimal.Parse(amountText, CultureInfo.InvariantCulture),
            CreatedDateTime = DateTimeOffset.UtcNow
        };

        Assert.Empty(CasinoWagerRequestValidator.Validate(request));
    }

    [Fact]
    public void IgnoresUnneededTesterFieldsWithoutChangingStoredContract()
    {
        const string payload = """
            {
              "wagerId": "aa6700eb-1a06-483e-9739-d293dc7a9383",
              "theme": "adventure",
              "provider": "Provider",
              "gameName": "Game",
              "accountId": "5ac75fec-23e9-27d1-b660-179eee70003d",
              "Username": "Player",
              "amount": 500,
              "createdDateTime": "2024-09-10T07:25:13.577Z",
              "transactionId": "410b7161-3473-4d74-85c3-a533d050a9d3",
              "duration": 1827254
            }
            """;

        var request = JsonSerializer.Deserialize<CasinoWagerRequest>(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal(500m, request.Amount);
        Assert.Empty(CasinoWagerRequestValidator.Validate(request));
    }
}
