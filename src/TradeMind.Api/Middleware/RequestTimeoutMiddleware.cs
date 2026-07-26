using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using TradeMind.Api.Composition;
using TradeMind.Api.Errors;

namespace TradeMind.Api.Middleware;

public sealed class RequestTimeoutMiddleware(
    RequestDelegate next,
    TimeProvider timeProvider,
    IOptions<ApiOptions> options)
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(options?.Value.Timeouts.RequestTimeoutSeconds
        ?? throw new ArgumentNullException(nameof(options)));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var lifetime = context.Features.Get<IHttpRequestLifetimeFeature>();
        var originalToken = context.RequestAborted;
        using var timeoutSource = new CancellationTokenSource(_timeout, timeProvider);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(originalToken, timeoutSource.Token);
        if (lifetime is not null)
        {
            lifetime.RequestAborted = linkedSource.Token;
        }

        try
        {
            await next(context).ConfigureAwait(false);
            if (timeoutSource.IsCancellationRequested && !originalToken.IsCancellationRequested)
            {
                throw new ApiRequestTimeoutException(_timeout);
            }
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !originalToken.IsCancellationRequested)
        {
            throw new ApiRequestTimeoutException(_timeout);
        }
        finally
        {
            if (lifetime is not null)
            {
                lifetime.RequestAborted = originalToken;
            }
        }
    }
}
