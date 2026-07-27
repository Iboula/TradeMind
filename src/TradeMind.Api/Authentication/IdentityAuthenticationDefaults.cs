namespace TradeMind.Api.Authentication;

public static class IdentityAuthenticationDefaults
{
    public const string PolicyScheme = "TradeMind.Identity";
    public const string JwtScheme = "TradeMind.Jwt";
    public const string ApiKeyScheme = "TradeMind.ApiKey";
    public const string ApiKeyHeader = "X-TradeMind-Api-Key";
}
