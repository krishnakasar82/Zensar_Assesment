using OT.Assessment.Domain;
using OT.Assessment.Shared;

namespace OT.Assessment.Application;

public interface ICasinoWagerService
{
    Task PublishAsync(CasinoWagerMessage wager, CancellationToken cancellationToken);

    Task<CasinoWagerPage> GetHistoryAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TopSpender>> GetTopSpendersAsync(
        int count,
        CancellationToken cancellationToken);
}
