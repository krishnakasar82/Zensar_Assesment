namespace OT.Assessment.Api.Endpoints;

/// <summary>Confirmation that a wager was accepted for asynchronous processing.</summary>
public sealed record WagerAcceptedResponse(Guid WagerId, string Status);

/// <summary>A wager returned in an account's history.</summary>
public sealed record CasinoWagerResponse(
    Guid WagerId,
    string Game,
    string Provider,
    decimal Amount,
    DateTimeOffset CreatedDate);

/// <summary>A page of wager history and its pagination metadata.</summary>
public sealed record CasinoWagerPageResponse(
    IReadOnlyList<CasinoWagerResponse> Data,
    int Page,
    int PageSize,
    long Total,
    long TotalPages);

/// <summary>An account and its aggregate wager amount.</summary>
public sealed record TopSpenderResponse(
    Guid AccountId,
    string Username,
    decimal TotalAmountSpend);
