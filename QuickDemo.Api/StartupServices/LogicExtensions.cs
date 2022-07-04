using QuickDemo.Logic;

namespace QuickDemo.Api.StartupServices;

public static class LogicExtensions
{
    public static IServiceCollection AddLogic(this IServiceCollection services)
    {
        services.AddScoped<IPostalCodeLogic, PostalCodeLogic>();
        return services;
    }
}
