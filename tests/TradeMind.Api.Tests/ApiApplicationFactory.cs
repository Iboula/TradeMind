using System.Collections.Concurrent;
using TradeMind.Api.Contracts.MarketContext;

namespace TradeMind.Api.Tests;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    public ApiApplicationFactory() : this(new FakeApiBehavior())
    {
    }

    internal ApiApplicationFactory(FakeApiBehavior behavior)
    {
        Behavior = behavior;
    }

    public FakeApiBehavior Behavior { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("TradeMind:Api:Security:EnableHttpsRedirection", "false");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:KnowledgeHub"] = string.Empty,
            ["ConnectionStrings:MarketConnectors"] = string.Empty
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITradeMindApiApplication>();
            services.AddScoped<ITradeMindApiApplication>(_ => new FakeTradeMindApiApplication(Behavior));
        });
    }
}

public sealed class FakeApiBehavior
{
    public ConcurrentQueue<ApiModule> Invocations { get; } = new();
    public int ExecutionCount => Volatile.Read(ref _executionCount);
    public bool ThrowUnexpectedException { get; set; }
    public bool ThrowCancellation { get; set; }
    public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool WaitForRelease { get; set; }
    private int _executionCount;

    public void Count(ApiModule module)
    {
        Interlocked.Increment(ref _executionCount);
        Invocations.Enqueue(module);
        Started.TrySetResult(true);
    }
}

public sealed class FakeTradeMindApiApplication(FakeApiBehavior behavior) : ITradeMindApiApplication
{
    public Task<ApiOperationResult> BuildMarketContextAsync(
        BuildMarketContextApiRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        behavior.Count(ApiModule.MarketContext);
        return Task.FromResult(Success(ApiModule.MarketContext, new { status = "fake", instrument = request.Instrument }));
    }

    public async Task<ApiOperationResult> ExecuteAsync(
        ApiModule module,
        JsonElement payload,
        string correlationId,
        CancellationToken cancellationToken)
    {
        behavior.Count(module);
        if (behavior.ThrowUnexpectedException) throw new InvalidOperationException("internal test failure");
        if (behavior.ThrowCancellation) throw new OperationCanceledException(cancellationToken);
        if (behavior.WaitForRelease)
        {
            await behavior.Release.Task.WaitAsync(cancellationToken);
        }

        return Success(module, new { module = module.ToString(), accepted = true });
    }

    private static ApiOperationResult Success(ApiModule module, object data) =>
        ApiOperationResult.Success(module, JsonSerializer.SerializeToElement(data, ApiJson.Options));
}
