using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TradeMind.Api.Composition;

namespace TradeMind.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    IOptions<ApiOptions> options,
    TimeProvider timeProvider)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "TradeMind.CorrelationId";
    private readonly int _maximumLength = options.Value.Correlation.MaximumLength;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsSafe(incoming, _maximumLength)
            ? incoming!
            : CreateCorrelationId(context, timeProvider);
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await next(context).ConfigureAwait(false);
    }

    private static bool IsSafe(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximumLength
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    private static string CreateCorrelationId(HttpContext context, TimeProvider timeProvider)
    {
        var seed = $"{context.TraceIdentifier}|{timeProvider.GetUtcNow():O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }
}
