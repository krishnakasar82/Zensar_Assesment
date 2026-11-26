using OT.Assessment.Application;

namespace OT.Assessment.Consumer;

public sealed class Worker(
    IWagerQueueConsumer queueConsumer,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Casino wager consumer starting.");
        await queueConsumer.RunAsync(stoppingToken);
        logger.LogInformation("Casino wager consumer stopped.");
    }
}
