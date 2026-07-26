using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TradeMind.Api.Composition;
using TradeMind.Api.Errors;

namespace TradeMind.Api.Middleware;

public interface IIdempotencyStore
{
    Task<IdempotencyExecutionResult> ExecuteAsync(
        string key,
        string requestHash,
        Func<CancellationToken, Task<CapturedResponse>> operation,
        CancellationToken cancellationToken);
}

public sealed record CapturedResponse(int StatusCode, string? ContentType, byte[] Body);

public sealed record IdempotencyExecutionResult(
    CapturedResponse Response,
    bool IsReplay,
    bool IsConflict,
    bool IsInProgress = false);

public sealed class InMemoryIdempotencyStore(
    IOptions<ApiOptions> options,
    TimeProvider timeProvider) : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(options.Value.Idempotency.TtlMinutes);

    public async Task<IdempotencyExecutionResult> ExecuteAsync(
        string key,
        string requestHash,
        Func<CancellationToken, Task<CapturedResponse>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentNullException.ThrowIfNull(operation);
        PruneExpired();

        while (true)
        {
            var now = timeProvider.GetUtcNow();
            var candidate = new Entry(requestHash, now.Add(_ttl));
            var entry = _entries.GetOrAdd(key, candidate);
            if (ReferenceEquals(entry, candidate))
            {
                try
                {
                    var response = await operation(cancellationToken).ConfigureAwait(false);
                    entry.Completion.TrySetResult(response);
                    return new IdempotencyExecutionResult(response, false, false);
                }
                catch (Exception exception)
                {
                    entry.Completion.TrySetException(exception);
                    _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
                    throw;
                }
            }

            if (entry.ExpiresAtUtc <= now)
            {
                _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
                continue;
            }

            if (!string.Equals(entry.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return new IdempotencyExecutionResult(
                    new CapturedResponse(StatusCodes.Status409Conflict, null, []),
                    false,
                    true);
            }

            var replay = await entry.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new IdempotencyExecutionResult(replay, true, false);
        }
    }

    private void PruneExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAtUtc <= now && pair.Value.Completion.Task.IsCompleted)
            {
                _entries.TryRemove(new KeyValuePair<string, Entry>(pair.Key, pair.Value));
            }
        }
    }

    private sealed class Entry(string requestHash, DateTimeOffset expiresAtUtc)
    {
        public string RequestHash { get; } = requestHash;
        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
        public TaskCompletionSource<CapturedResponse> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    IIdempotencyStore store,
    IOptions<ApiOptions> options,
    ILogger<IdempotencyMiddleware> logger)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayItemKey = "TradeMind.IdempotencyReplay";
    private readonly ApiOptions _apiOptions = options.Value;
    private readonly IdempotencyOptions _options = options.Value.Idempotency;

    public async Task InvokeAsync(HttpContext context)
    {
        var endpointRequiresIdempotency = context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyMetadata.Required>() is not null;
        if (!endpointRequiresIdempotency || !HttpMethods.IsPost(context.Request.Method) || !_options.Enabled)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var key = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!IsValidKey(key))
        {
            await ApiProblemDetails.WriteAsync(context, StatusCodes.Status400BadRequest, "Invalid idempotency key", "A valid Idempotency-Key header is required for this operation.", [new("INVALID_IDEMPOTENCY_KEY", "The key must contain only safe ASCII characters and fit the configured length limit.", HeaderName)]).ConfigureAwait(false);
            return;
        }

        if (context.Request.ContentLength > _apiOptions.PayloadLimits.MaximumBodyBytes)
        {
            await ApiProblemDetails.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, "Payload too large", "The request body exceeds the configured API limit.").ConfigureAwait(false);
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        context.Request.Body.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var result = await store.ExecuteAsync(
            key!,
            hash,
            cancellationToken => CaptureResponseAsync(context, cancellationToken),
            context.RequestAborted).ConfigureAwait(false);
        if (result.IsConflict)
        {
            logger.LogWarning("Idempotency key conflict. RequestHashPrefix={RequestHashPrefix}, CorrelationId={CorrelationId}", hash[..12], context.Items[CorrelationIdMiddleware.ItemKey]);
            await ApiProblemDetails.WriteAsync(context, StatusCodes.Status409Conflict, "Idempotency key conflict", "The idempotency key was already used with a different request payload.").ConfigureAwait(false);
            return;
        }

        if (result.IsReplay)
        {
            context.Items[ReplayItemKey] = true;
            logger.LogInformation("Idempotency response replayed. RequestHashPrefix={RequestHashPrefix}, CorrelationId={CorrelationId}", hash[..12], context.Items[CorrelationIdMiddleware.ItemKey]);
        }

        if (result.IsInProgress)
        {
            await ApiProblemDetails.WriteAsync(context, StatusCodes.Status409Conflict, "Idempotency operation in progress", "The idempotency key is currently being processed. Retry after the original operation completes.").ConfigureAwait(false);
            return;
        }

        await WriteCapturedResponseAsync(context, result.Response).ConfigureAwait(false);
    }

    private async Task<CapturedResponse> CaptureResponseAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await next(context).ConfigureAwait(false);
            return new CapturedResponse(
                context.Response.StatusCode,
                context.Response.ContentType,
                buffer.ToArray());
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static async Task WriteCapturedResponseAsync(HttpContext context, CapturedResponse response)
    {
        context.Response.StatusCode = response.StatusCode;
        if (!string.IsNullOrWhiteSpace(response.ContentType)) context.Response.ContentType = response.ContentType;
        await context.Response.Body.WriteAsync(response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= _options.MaximumKeyLength
        && key.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

}
