using Microsoft.AspNetCore.OpenApi;

namespace TradeMind.Api.OpenApi;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddTradeMindOpenApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue("TradeMind:Api:OpenApi:Enabled", true))
        {
            services.AddOpenApi("v1", options =>
            {
                options.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = "TradeMind API";
                    document.Info.Version = "v1";
                    document.Info.Description = "HTTP composition host for the deterministic TradeMind analytical core.";
                    return Task.CompletedTask;
                });
            });
        }

        return services;
    }
}
