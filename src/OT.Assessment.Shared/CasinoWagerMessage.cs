namespace OT.Assessment.Shared;

public sealed record CasinoWagerMessage
{
    public required Guid WagerId { get; init; }
    public required Guid AccountId { get; init; }
    public required string Username { get; init; }
    public required string Provider { get; init; }
    public required string GameName { get; init; }
    public required decimal Amount { get; init; }
    public required DateTimeOffset CreatedDateTime { get; init; }
}
