using RabbitMQ.Client;

namespace OT.Assessment.Infrastructure;

internal static class RabbitMqTopology
{
    public const string RoutingKey = "wager";
    public const string RetryRoutingKey = "retry";
    public const string DeadLetterRoutingKey = "dead";

    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            options.ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            options.RetryExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            options.DeadLetterExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            options.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = options.RetryDelayMilliseconds,
                ["x-dead-letter-exchange"] = options.ExchangeName,
                ["x-dead-letter-routing-key"] = RoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            options.QueueName,
            options.ExchangeName,
            RoutingKey,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.RetryQueueName,
            options.RetryExchangeName,
            RetryRoutingKey,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            options.DeadLetterQueueName,
            options.DeadLetterExchangeName,
            DeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }
}
