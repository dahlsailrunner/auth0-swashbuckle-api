using System.IdentityModel.Tokens.Jwt;
using System.Net;
using KnowYourToolset.ApiComponents.Middleware;
using QuickDemo.Api.LogEnrichers;
using QuickDemo.Api.StartupServices;
using QuickDemo.Api.Swagger;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Serilog;
using Serilog.Core;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up");

try
{
    var builder = WebApplication.CreateBuilder(args);
    var configuration = builder.Configuration;
   
    builder.Services.AddControllers();
    builder.Services.AddHealthChecks();

    builder.Services.AddLogic()// defined in StartupServices folder
        .AddCustomApiVersioning()
        .AddSwaggerFeatures()
        .AddTransient<ILogEventEnricher, StandardEnricher>()
        .AddHttpContextAccessor();

    builder.Services
        .AddMvcCore(options => { options.AddBaseAuthorizationFilters(configuration); }) //                .AddCors()
        .AddApiExplorer();

    JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
    builder.Services
        .AddAuthentication("Bearer")
        .AddJwtBearer(options =>
        {
            options.Authority = configuration.GetValue<string>("Authentication:Authority");
            options.Audience = configuration.GetValue<string>("Authentication:ApiName");
        });

    builder.Host.UseSerilog(((context, services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "QuickDemo.Api") // or entry assembly name
            .WriteTo.Console();
    }));

    var app = builder.Build();


    // Configure the HTTP request pipeline.
    app.UseProblemDetailsHandler(options => options.AddResponseDetails = CustomizeResponse);

    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
    {
        var forwardedHeaderOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        forwardedHeaderOptions.KnownNetworks.Clear();
        forwardedHeaderOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeaderOptions);
    }

    var corsOrigins = configuration.GetValue<string>("CORSOrigins")?.Split(",");
    if (corsOrigins!= null && corsOrigins.Any())
    {
        app.UseCors(bld => bld
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
    }

    var apiVersionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

    app
        .UseSwaggerFeatures(configuration, apiVersionProvider, app.Environment)
        .UseAuthentication()
        .UseCustomRequestLogging()
        .UseRouting()
        .UseAuthorization()
        .UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health");
            endpoints.MapFallback(() => Results.Redirect("/swagger"));
        });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}


HttpStatusCode CustomizeResponse(HttpContext ctx, Exception ex, ProblemDetails problemDetails)
{
    var httpStatus = HttpStatusCode.InternalServerError;
    if (ex is ApplicationException appEx)
    {
        problemDetails.Detail = appEx.Message;
        httpStatus = HttpStatusCode.BadRequest;
    }

    return httpStatus;
}
