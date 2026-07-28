using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TradeMind.Identity.Application;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Identity.Infrastructure.Security;
using TradeMind.Observability.Abstractions;

namespace TradeMind.Api.Authentication;

#pragma warning disable CS0618
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TimeProvider timeProvider,
    IApiKeyCredentialValidator validator,
    IOptions<IdentityOptions> identityOptions,
    ITradeMindMetrics metrics) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder, new TimeProviderSystemClock(timeProvider))
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!identityOptions.Value.Enabled || !identityOptions.Value.ApiKeys.Enabled)
            return AuthenticateResult.NoResult();
        var headerName = identityOptions.Value.ApiKeys.HeaderName;
        if (!Request.Headers.TryGetValue(headerName, out var values) || string.IsNullOrWhiteSpace(values.SingleOrDefault())) return AuthenticateResult.NoResult();
        var raw = values.SingleOrDefault()!;
        metrics.IncrementCounter(TelemetryMetricNames.AuthenticationAttempts, 1, new MetricDimensions(AuthenticationMethod: "api_key"));
        if (!ApiKeySecretGenerator.TryParse(raw, out var publicKeyId, out var secretPart))
        {
            metrics.IncrementCounter(TelemetryMetricNames.AuthenticationFailures, 1, new MetricDimensions(AuthenticationMethod: "api_key", Outcome: "Rejected"));
            return AuthenticateResult.Fail("Invalid credentials.");
        }
        try
        {
            var actor = await validator.ValidateAsync(publicKeyId, secretPart, Context.RequestAborted).ConfigureAwait(false);
            if (actor is null)
            {
                metrics.IncrementCounter(TelemetryMetricNames.AuthenticationFailures, 1, new MetricDimensions(AuthenticationMethod: "api_key", Outcome: "Rejected"));
                return AuthenticateResult.Fail("Invalid credentials.");
            }
            Context.Items[IdentityHttpContextKeys.Actor] = actor;
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, actor.ActorId),
                new("sub", actor.ActorId),
                new("organization_id", actor.OrganizationId!.Value.Value),
                new("tenant_id", actor.TenantId!.Value.Value),
                new("tm_actor_type", actor.ActorType.ToString()),
                new("tm_api_key_id", actor.ApiKeyId!.Value.ToString())
            };
            claims.AddRange(actor.Permissions.Values.Select(permission => new Claim("permission", permission.Value)));
            var identity = new ClaimsIdentity(claims, IdentityAuthenticationDefaults.ApiKeyScheme);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), IdentityAuthenticationDefaults.ApiKeyScheme));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            metrics.IncrementCounter(TelemetryMetricNames.AuthenticationFailures, 1, new MetricDimensions(AuthenticationMethod: "api_key", Outcome: "Rejected"));
            return AuthenticateResult.Fail("Invalid credentials.");
        }
    }

    private sealed class TimeProviderSystemClock(TimeProvider timeProvider) : ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
#pragma warning restore CS0618
