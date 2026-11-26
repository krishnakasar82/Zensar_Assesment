using OT.Assessment.Domain;
using OT.Assessment.Shared;

namespace OT.Assessment.Application;

public sealed class CasinoWagerService(
    IWagerPublisher publisher,
    ICasinoWagerRepository repository) : ICasinoWagerService
{
    public Task PublishAsync(CasinoWagerMessage wager, CancellationToken cancellationToken) =>
        publisher.PublishAsync(wager, cancellationToken);

    public Task<CasinoWagerPage> GetHistoryAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        repository.GetHistoryAsync(accountId, page, pageSize, cancellationToken);

    public Task<IReadOnlyList<TopSpender>> GetTopSpendersAsync(
        int count,
        CancellationToken cancellationToken) =>
        repository.GetTopSpendersAsync(count, cancellationToken);
}
