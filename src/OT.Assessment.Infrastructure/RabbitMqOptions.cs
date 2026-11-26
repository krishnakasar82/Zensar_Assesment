namespace OT.Assessment.Infrastructure;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string QueueName { get; set; } = "casino.wagers";
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 5000;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName)
            || Port is < 1 or > 65535
            || string.IsNullOrWhiteSpace(UserName)
            || string.IsNullOrWhiteSpace(QueueName)
            || MaxRetries < 0
            || RetryDelayMilliseconds < 1)
        {
            throw new InvalidOperationException("RabbitMq configuration is invalid.");
        }
    }

    public string ExchangeName => $"{QueueName}.exchange";
    public string RetryExchangeName => $"{QueueName}.retry.exchange";
    public string RetryQueueName => $"{QueueName}.retry";
    public string DeadLetterExchangeName => $"{QueueName}.dead.exchange";
    public string DeadLetterQueueName => $"{QueueName}.dead";
}
