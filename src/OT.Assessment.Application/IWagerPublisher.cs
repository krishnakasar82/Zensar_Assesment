using OT.Assessment.Shared;

namespace OT.Assessment.Application;

public interface IWagerPublisher
{
    Task PublishAsync(CasinoWagerMessage wager, CancellationToken cancellationToken);
}
