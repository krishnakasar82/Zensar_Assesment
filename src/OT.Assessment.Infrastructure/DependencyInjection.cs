using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OT.Assessment.Application;

namespace OT.Assessment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAssessmentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must be configured.");
        }

        var rabbitMqOptions = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
        rabbitMqOptions.Validate();

        services.AddSingleton(rabbitMqOptions);
        services.AddSingleton<ICasinoWagerRepository>(
            _ => new SqlCasinoWagerRepository(connectionString));
        services.AddSingleton<IWagerPublisher, RabbitMqWagerPublisher>();
        services.AddSingleton<IWagerQueueConsumer, RabbitMqWagerConsumer>();
        return services;
    }
}
