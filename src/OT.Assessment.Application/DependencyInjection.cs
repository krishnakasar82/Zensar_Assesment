using Microsoft.Extensions.DependencyInjection;

namespace OT.Assessment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAssessmentApplication(this IServiceCollection services)
    {
        services.AddScoped<ICasinoWagerService, CasinoWagerService>();
        return services;
    }
}
