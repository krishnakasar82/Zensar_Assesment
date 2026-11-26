namespace OT.Assessment.Domain;

public sealed record CasinoWager(
    Guid WagerId,
    Guid AccountId,
    string GameName,
    string Provider,
    decimal Amount,
    DateTimeOffset CreatedDateTime);

public sealed record TopSpender(
    Guid AccountId,
    string Username,
    decimal TotalAmountSpend);

public sealed record CasinoWagerPage(
    IReadOnlyList<CasinoWager> Data,
    int Page,
    int PageSize,
    long Total,
    long TotalPages);
