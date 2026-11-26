using System.Text.Json.Serialization;

namespace OT.Assessment.Api.Endpoints;

/// <summary>Payload for submitting a casino wager.</summary>
public sealed record CasinoWagerRequest
{
    [JsonPropertyName("wagerId")]
    public required Guid WagerId { get; init; }

    [JsonPropertyName("accountId")]
    public required Guid AccountId { get; init; }

    [JsonPropertyName("Username")]
    public required string Username { get; init; }

    [JsonPropertyName("provider")]
    public required string Provider { get; init; }

    [JsonPropertyName("gameName")]
    public required string GameName { get; init; }

    [JsonPropertyName("amount")]
    public required decimal Amount { get; init; }

    [JsonPropertyName("createdDateTime")]
    public required DateTimeOffset CreatedDateTime { get; init; }
}

/// <summary>Validation rules for wager submissions.</summary>
public static class CasinoWagerRequestValidator
{
    public static Dictionary<string, string[]> Validate(CasinoWagerRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        AddIf(request.WagerId == Guid.Empty, "wagerId", "A non-empty wagerId is required.");
        AddIf(request.AccountId == Guid.Empty, "accountId", "A non-empty accountId is required.");
        AddIf(string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100,
            "Username", "Username must contain between 1 and 100 characters.");
        AddIf(string.IsNullOrWhiteSpace(request.Provider) || request.Provider.Length > 200,
            "provider", "Provider must contain between 1 and 200 characters.");
        AddIf(string.IsNullOrWhiteSpace(request.GameName) || request.GameName.Length > 200,
            "gameName", "Game name must contain between 1 and 200 characters.");
        AddIf(request.Amount <= 0m || request.Amount > 99999999999.99999999999999999m,
            "amount", "Amount must be greater than zero and fit the supported precision.");
        AddIf(request.CreatedDateTime == default,
            "createdDateTime", "A valid createdDateTime is required.");

        return errors;

        void AddIf(bool condition, string field, string message)
        {
            if (condition)
            {
                errors[field] = [message];
            }
        }
    }
}
