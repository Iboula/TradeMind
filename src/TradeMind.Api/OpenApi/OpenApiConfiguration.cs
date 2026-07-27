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
                    document.Components ??= new();
                    document.Components.SecuritySchemes["Bearer"] = new()
                    {
                        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "JWT bearer token issued by the configured identity authority."
                    };
                    document.Components.SecuritySchemes["TradeMindApiKey"] = new()
                    {
                        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                        Name = "X-TradeMind-Api-Key",
                        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                        Description = "Scoped TradeMind API key. The raw secret is shown only once when the key is created."
                    };
                    return Task.CompletedTask;
                });
            });
        }

        return services;
    }
}
