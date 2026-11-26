namespace OT.Assessment.Application;

public interface IWagerQueueConsumer
{
    Task RunAsync(CancellationToken cancellationToken);
}
