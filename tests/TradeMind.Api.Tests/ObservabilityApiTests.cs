using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeMind.Api.Middleware;
using TradeMind.Identity.Application.Abstractions;
using TradeMind.Observability.Abstractions;
using TradeMind.Observability.OpenTelemetry;

namespace TradeMind.Api.Tests;

public sealed class ObservabilityApiTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public ObservabilityApiTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Startup_health_is_minimal_and_healthy_after_host_start()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/startup");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("startup", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionStrings", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Readiness_health_is_redacted()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ready", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionStrings", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Prometheus_endpoint_is_available_and_does_not_expose_tenant_dimensions()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("trademind_api_requests", body, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant_id=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session_id=", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Correlation_and_execution_session_headers_remain_transport_metadata()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "observability-test");
        var response = await client.GetAsync("/api/v1/system/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("observability-test", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Request_telemetry_middleware_records_success_rejection_failure_and_cancellation()
    {
        using var scope = _factory.Services.CreateScope();
        foreach (var statusCode in new[] { StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status500InternalServerError })
        {
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Items[CorrelationIdMiddleware.ItemKey] = "middleware-test";
            var middleware = new RequestTelemetryMiddleware(
                _ =>
                {
                    context.Response.StatusCode = statusCode;
                    return Task.CompletedTask;
                },
                scope.ServiceProvider.GetRequiredService<ITelemetryContextAccessor>(),
                scope.ServiceProvider.GetRequiredService<ITradeMindTelemetry>(),
                scope.ServiceProvider.GetRequiredService<ITradeMindMetrics>(),
                scope.ServiceProvider.GetRequiredService<ICurrentActor>(),
                scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenTelemetryOptions>>(),
                scope.ServiceProvider.GetRequiredService<ILogger<RequestTelemetryMiddleware>>());

            await middleware.InvokeAsync(context);
            Assert.Equal(statusCode, context.Response.StatusCode);
        }

        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelledContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        cancelledContext.RequestAborted = cancellation.Token;
        var cancelledMiddleware = new RequestTelemetryMiddleware(
            _ => Task.FromCanceled(cancellation.Token),
            scope.ServiceProvider.GetRequiredService<ITelemetryContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<ITradeMindTelemetry>(),
            scope.ServiceProvider.GetRequiredService<ITradeMindMetrics>(),
            scope.ServiceProvider.GetRequiredService<ICurrentActor>(),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenTelemetryOptions>>(),
            scope.ServiceProvider.GetRequiredService<ILogger<RequestTelemetryMiddleware>>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledMiddleware.InvokeAsync(cancelledContext));

        var failedMiddleware = new RequestTelemetryMiddleware(
            _ => throw new InvalidOperationException("middleware failure"),
            scope.ServiceProvider.GetRequiredService<ITelemetryContextAccessor>(),
            scope.ServiceProvider.GetRequiredService<ITradeMindTelemetry>(),
            scope.ServiceProvider.GetRequiredService<ITradeMindMetrics>(),
            scope.ServiceProvider.GetRequiredService<ICurrentActor>(),
            scope.ServiceProvider.GetRequiredService<ICurrentTenant>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenTelemetryOptions>>(),
            scope.ServiceProvider.GetRequiredService<ILogger<RequestTelemetryMiddleware>>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failedMiddleware.InvokeAsync(new DefaultHttpContext { RequestServices = scope.ServiceProvider }));
    }
}
