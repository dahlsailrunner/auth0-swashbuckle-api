using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace QuickDemo.Api.Swagger;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerFeatures(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen();

        return services;
    }

    public static IApplicationBuilder UseSwaggerFeatures(this IApplicationBuilder app, IConfiguration config,
        IApiVersionDescriptionProvider provider, IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return app;
        }

        var clientId = config.GetValue<string>("Authentication:SwaggerClientId");
        app
            .UseSwagger()
            .UseSwaggerUI(options =>
            {
                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                        $"QuickDemo API {description.GroupName.ToUpperInvariant()}");
                    options.RoutePrefix = string.Empty;
                }

                options.DocumentTitle = "QuickDemo Documentation";
                options.OAuthClientId(clientId);
                options.OAuthAppName("QuickDemo");
                options.OAuthUsePkce();
                options.OAuthAdditionalQueryStringParams(new Dictionary<string, string>
                {
                    { "audience", config.GetValue<string>("Authentication:ApiName") }
                });
            });

        return app;
    }
}
