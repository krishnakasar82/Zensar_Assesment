namespace OT.Assessment.Application;

public sealed class BrokerUnavailableException(string message, Exception innerException)
    : Exception(message, innerException)
{
}
