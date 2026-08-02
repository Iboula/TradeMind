using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TradeMind.Brokers.MetaTrader5.Bridge.Contracts.Protocol;

public static class BridgePayloadLimits
{
    public const int MaxPayloadBytes = 64 * 1024;
    public const int MaxFields = 64;
    public const int MaxFieldNameLength = 64;
    public const int MaxFieldValueLength = 4096;
    public const int MaxMessageLength = 512;
    public const int MaxCollectionItems = 256;
    public const int MaxNonceLength = 128;
}

public sealed record BridgeProtocolVersion
{
    public BridgeProtocolVersion(int major, int minor)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
        Major = major;
        Minor = minor;
    }

    public int Major { get; }
    public int Minor { get; }
    public static BridgeProtocolVersion Current { get; } = new(1, 0);
    public bool IsCompatibleWith(BridgeProtocolVersion supported) => Major == supported.Major && Minor <= supported.Minor;
    public override string ToString() => $"{Major}.{Minor}";
}

public enum BridgeOperation
{
    Handshake,
    Health,
    Account,
    Instrument,
    SubmitOrder,
    ModifyOrder,
    CancelOrder,
    ClosePosition,
    Orders,
    Positions,
    Heartbeat
}

public enum BridgeErrorCode
{
    ProtocolVersionMismatch,
    AuthenticationFailed,
    AuthorizationFailed,
    ReplayDetected,
    ClockSkewExceeded,
    BridgeUnavailable,
    TerminalUnavailable,
    TerminalNotReady,
    DemoModeRequired,
    LiveModeForbidden,
    InvalidRequest,
    DuplicateRequest,
    Conflict,
    Timeout,
    Cancelled,
    TransportFailure,
    ProtocolFailure,
    TerminalRejected,
    Unknown
}

public sealed record BridgeError
{
    public BridgeError(BridgeErrorCode code, string message, bool retryable, bool safeToRetry)
    {
        Code = code;
        Message = BridgeValidation.Message(message);
        Retryable = retryable;
        SafeToRetry = safeToRetry;
    }

    public BridgeErrorCode Code { get; }
    public string Message { get; }
    public bool Retryable { get; }
    public bool SafeToRetry { get; }
}

public sealed record BridgeCapability
{
    public BridgeCapability(string name, string version)
    {
        Name = BridgeValidation.Required(name, nameof(name), BridgePayloadLimits.MaxFieldNameLength);
        Version = BridgeValidation.Required(version, nameof(version), 32);
    }

    public string Name { get; }
    public string Version { get; }
}

public sealed record BridgeRequestMetadata
{
    public BridgeRequestMetadata(
        string requestId,
        string correlationId,
        string executionSessionId,
        DateTimeOffset timestampUtc,
        string nonce,
        string idempotencyKeyHash,
        string normalizedRequestHash,
        DateTimeOffset deadlineUtc)
    {
        RequestId = BridgeValidation.Required(requestId, nameof(requestId), 128);
        CorrelationId = BridgeValidation.Required(correlationId, nameof(correlationId), 128);
        ExecutionSessionId = BridgeValidation.Required(executionSessionId, nameof(executionSessionId), 128);
        TimestampUtc = BridgeValidation.Utc(timestampUtc, nameof(timestampUtc));
        Nonce = BridgeValidation.Required(nonce, nameof(nonce), BridgePayloadLimits.MaxNonceLength);
        if (Nonce.Length < 16) throw new ArgumentException("Nonce must contain at least 16 characters.", nameof(nonce));
        IdempotencyKeyHash = BridgeValidation.Sha256(idempotencyKeyHash, nameof(idempotencyKeyHash));
        NormalizedRequestHash = BridgeValidation.Sha256(normalizedRequestHash, nameof(normalizedRequestHash));
        DeadlineUtc = BridgeValidation.Utc(deadlineUtc, nameof(deadlineUtc));
        if (DeadlineUtc <= TimestampUtc) throw new ArgumentException("Deadline must be after the request timestamp.", nameof(deadlineUtc));
    }

    public string RequestId { get; }
    public string CorrelationId { get; }
    public string ExecutionSessionId { get; }
    public DateTimeOffset TimestampUtc { get; }
    public string Nonce { get; }
    public string IdempotencyKeyHash { get; }
    public string NormalizedRequestHash { get; }
    public DateTimeOffset DeadlineUtc { get; }
}

public sealed record BridgeResponseMetadata
{
    public BridgeResponseMetadata(string requestId, string correlationId, string executionSessionId, DateTimeOffset serverTimeUtc)
    {
        RequestId = BridgeValidation.Required(requestId, nameof(requestId), 128);
        CorrelationId = BridgeValidation.Required(correlationId, nameof(correlationId), 128);
        ExecutionSessionId = BridgeValidation.Required(executionSessionId, nameof(executionSessionId), 128);
        ServerTimeUtc = BridgeValidation.Utc(serverTimeUtc, nameof(serverTimeUtc));
    }

    public string RequestId { get; }
    public string CorrelationId { get; }
    public string ExecutionSessionId { get; }
    public DateTimeOffset ServerTimeUtc { get; }
}

public sealed record BridgeTransportRequest
{
    public BridgeTransportRequest(BridgeProtocolVersion protocolVersion, BridgeOperation operation, BridgeRequestMetadata metadata, string payloadJson)
    {
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
        Operation = operation;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        PayloadJson = BridgeValidation.Payload(payloadJson);
    }

    public BridgeProtocolVersion ProtocolVersion { get; }
    public BridgeOperation Operation { get; }
    public BridgeRequestMetadata Metadata { get; }
    public string PayloadJson { get; }
}

public sealed record BridgeTransportResponse
{
    public BridgeTransportResponse(BridgeProtocolVersion protocolVersion, BridgeResponseMetadata metadata, string payloadJson, BridgeError? error)
    {
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        PayloadJson = BridgeValidation.Payload(payloadJson);
        Error = error;
    }

    public BridgeProtocolVersion ProtocolVersion { get; }
    public BridgeResponseMetadata Metadata { get; }
    public string PayloadJson { get; }
    public BridgeError? Error { get; }
    public bool Succeeded => Error is null;
}

public sealed record BridgeOrderData
{
    public BridgeOrderData(string accountId, string instrument, string side, string orderType, decimal quantity, decimal? requestedPrice, string timeInForce)
    {
        AccountId = BridgeValidation.Required(accountId, nameof(accountId), 128);
        Instrument = BridgeValidation.Required(instrument, nameof(instrument), 64);
        Side = BridgeValidation.Required(side, nameof(side), 32);
        OrderType = BridgeValidation.Required(orderType, nameof(orderType), 32);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
        if (requestedPrice is <= 0) throw new ArgumentOutOfRangeException(nameof(requestedPrice));
        RequestedPrice = requestedPrice;
        TimeInForce = BridgeValidation.Required(timeInForce, nameof(timeInForce), 32);
    }

    public string AccountId { get; }
    public string Instrument { get; }
    public string Side { get; }
    public string OrderType { get; }
    public decimal Quantity { get; }
    public decimal? RequestedPrice { get; }
    public string TimeInForce { get; }
}

public sealed record BridgeOrderDataResult
{
    public BridgeOrderDataResult(string orderId, string accountId, string instrument, string side, string orderType, decimal quantity, string status, DateTimeOffset createdUtc)
    {
        OrderId = BridgeValidation.Required(orderId, nameof(orderId), 128);
        AccountId = BridgeValidation.Required(accountId, nameof(accountId), 128);
        Instrument = BridgeValidation.Required(instrument, nameof(instrument), 64);
        Side = BridgeValidation.Required(side, nameof(side), 32);
        OrderType = BridgeValidation.Required(orderType, nameof(orderType), 32);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
        Status = BridgeValidation.Required(status, nameof(status), 32);
        CreatedUtc = BridgeValidation.Utc(createdUtc, nameof(createdUtc));
    }

    public string OrderId { get; }
    public string AccountId { get; }
    public string Instrument { get; }
    public string Side { get; }
    public string OrderType { get; }
    public decimal Quantity { get; }
    public string Status { get; }
    public DateTimeOffset CreatedUtc { get; }
}

public sealed record BridgePositionDataResult
{
    public BridgePositionDataResult(string positionId, string accountId, string instrument, string side, decimal quantity, decimal openPrice, string status)
    {
        PositionId = BridgeValidation.Required(positionId, nameof(positionId), 128);
        AccountId = BridgeValidation.Required(accountId, nameof(accountId), 128);
        Instrument = BridgeValidation.Required(instrument, nameof(instrument), 64);
        Side = BridgeValidation.Required(side, nameof(side), 32);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
        if (openPrice <= 0) throw new ArgumentOutOfRangeException(nameof(openPrice));
        OpenPrice = openPrice;
        Status = BridgeValidation.Required(status, nameof(status), 32);
    }

    public string PositionId { get; }
    public string AccountId { get; }
    public string Instrument { get; }
    public string Side { get; }
    public decimal Quantity { get; }
    public decimal OpenPrice { get; }
    public string Status { get; }
}

public sealed record BridgeExecutionDataResult
{
    public BridgeExecutionDataResult(string executionId, string orderId, string instrument, decimal quantity, decimal fillPrice, DateTimeOffset executedUtc)
    {
        ExecutionId = BridgeValidation.Required(executionId, nameof(executionId), 128);
        OrderId = BridgeValidation.Required(orderId, nameof(orderId), 128);
        Instrument = BridgeValidation.Required(instrument, nameof(instrument), 64);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
        if (fillPrice <= 0) throw new ArgumentOutOfRangeException(nameof(fillPrice));
        FillPrice = fillPrice;
        ExecutedUtc = BridgeValidation.Utc(executedUtc, nameof(executedUtc));
    }

    public string ExecutionId { get; }
    public string OrderId { get; }
    public string Instrument { get; }
    public decimal Quantity { get; }
    public decimal FillPrice { get; }
    public DateTimeOffset ExecutedUtc { get; }
}

public sealed record BridgeAccountData
{
    public BridgeAccountData(string accountId, string environment, string accountType, string currency, decimal balance, decimal equity)
    {
        AccountId = BridgeValidation.Required(accountId, nameof(accountId), 128);
        Environment = BridgeValidation.Required(environment, nameof(environment), 32);
        AccountType = BridgeValidation.Required(accountType, nameof(accountType), 32);
        Currency = BridgeValidation.Required(currency, nameof(currency), 16);
        Balance = balance;
        Equity = equity;
    }

    public string AccountId { get; }
    public string Environment { get; }
    public string AccountType { get; }
    public string Currency { get; }
    public decimal Balance { get; }
    public decimal Equity { get; }
}

public sealed record BridgeInstrumentData
{
    public BridgeInstrumentData(string instrument, string assetClass, string marketStatus, int priceScale)
    {
        Instrument = BridgeValidation.Required(instrument, nameof(instrument), 64);
        AssetClass = BridgeValidation.Required(assetClass, nameof(assetClass), 32);
        MarketStatus = BridgeValidation.Required(marketStatus, nameof(marketStatus), 32);
        if (priceScale is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(priceScale));
        PriceScale = priceScale;
    }

    public string Instrument { get; }
    public string AssetClass { get; }
    public string MarketStatus { get; }
    public int PriceScale { get; }
}

public sealed record BridgeHandshakeRequest
{
    public BridgeHandshakeRequest(BridgeProtocolVersion protocolVersion, string adapterVersion, bool demoOnly, string environment, string accountEnvironment, DateTimeOffset clientTimeUtc, IReadOnlyList<BridgeCapability> requiredCapabilities, BridgeRequestMetadata metadata)
    {
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
        AdapterVersion = BridgeValidation.Required(adapterVersion, nameof(adapterVersion), 32);
        DemoOnly = demoOnly;
        Environment = BridgeValidation.Required(environment, nameof(environment), 32);
        AccountEnvironment = BridgeValidation.Required(accountEnvironment, nameof(accountEnvironment), 32);
        ClientTimeUtc = BridgeValidation.Utc(clientTimeUtc, nameof(clientTimeUtc));
        RequiredCapabilities = BridgeValidation.List(requiredCapabilities, nameof(requiredCapabilities));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public BridgeProtocolVersion ProtocolVersion { get; }
    public string AdapterVersion { get; }
    public bool DemoOnly { get; }
    public string Environment { get; }
    public string AccountEnvironment { get; }
    public DateTimeOffset ClientTimeUtc { get; }
    public IReadOnlyList<BridgeCapability> RequiredCapabilities { get; }
    public BridgeRequestMetadata Metadata { get; }
}

public sealed record BridgeHandshakeResponse
{
    public BridgeHandshakeResponse(BridgeProtocolVersion protocolVersion, BridgeResponseMetadata metadata, string bridgeVersion, string adapterVersion, bool demoOnly, string environment, string accountEnvironment, string terminalState, string terminalBuild, DateTimeOffset serverTimeUtc, double clockSkewSeconds, IReadOnlyList<BridgeCapability> capabilities, BridgeError? error)
    {
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        BridgeVersion = BridgeValidation.Required(bridgeVersion, nameof(bridgeVersion), 32);
        AdapterVersion = BridgeValidation.Required(adapterVersion, nameof(adapterVersion), 32);
        DemoOnly = demoOnly;
        Environment = BridgeValidation.Required(environment, nameof(environment), 32);
        AccountEnvironment = BridgeValidation.Required(accountEnvironment, nameof(accountEnvironment), 32);
        TerminalState = BridgeValidation.Required(terminalState, nameof(terminalState), 32);
        TerminalBuild = BridgeValidation.Required(terminalBuild, nameof(terminalBuild), 64);
        ServerTimeUtc = BridgeValidation.Utc(serverTimeUtc, nameof(serverTimeUtc));
        if (double.IsNaN(clockSkewSeconds) || double.IsInfinity(clockSkewSeconds)) throw new ArgumentOutOfRangeException(nameof(clockSkewSeconds));
        ClockSkewSeconds = clockSkewSeconds;
        Capabilities = BridgeValidation.List(capabilities, nameof(capabilities));
        Error = error;
    }

    public BridgeProtocolVersion ProtocolVersion { get; }
    public BridgeResponseMetadata Metadata { get; }
    public string BridgeVersion { get; }
    public string AdapterVersion { get; }
    public bool DemoOnly { get; }
    public string Environment { get; }
    public string AccountEnvironment { get; }
    public string TerminalState { get; }
    public string TerminalBuild { get; }
    public DateTimeOffset ServerTimeUtc { get; }
    public double ClockSkewSeconds { get; }
    public IReadOnlyList<BridgeCapability> Capabilities { get; }
    public BridgeError? Error { get; }
    public bool Succeeded => Error is null;
}

public sealed record BridgeHealthRequest(BridgeRequestMetadata Metadata);
public sealed record BridgeHealthResponse(BridgeResponseMetadata Metadata, string HostState, string TerminalState, BridgeProtocolVersion ProtocolVersion, string BridgeVersion, bool DemoOnly, TimeSpan HeartbeatAge, int ReconnectCount, double LatencyMilliseconds, DateTimeOffset? LastSuccessfulHandshakeUtc, BridgeError? Error);
public sealed record BridgeAccountRequest(BridgeRequestMetadata Metadata);
public sealed record BridgeAccountResponse(BridgeResponseMetadata Metadata, ImmutableArray<BridgeAccountData> Accounts, BridgeError? Error);
public sealed record BridgeInstrumentRequest(BridgeRequestMetadata Metadata, string Instrument);
public sealed record BridgeInstrumentResponse(BridgeResponseMetadata Metadata, BridgeInstrumentData? Instrument, BridgeError? Error);
public sealed record BridgeSubmitOrderRequest(BridgeRequestMetadata Metadata, BridgeOrderData Order, bool DemoOnly);
public sealed record BridgeSubmitOrderResponse(BridgeResponseMetadata Metadata, BridgeOrderDataResult? Order, BridgeExecutionDataResult? Execution, BridgeError? Error);
public sealed record BridgeModifyOrderRequest(BridgeRequestMetadata Metadata, string OrderId, decimal? Quantity, decimal? LimitPrice, decimal? StopPrice, bool DemoOnly);
public sealed record BridgeModifyOrderResponse(BridgeResponseMetadata Metadata, BridgeOrderDataResult? Order, BridgeError? Error);
public sealed record BridgeCancelOrderRequest(BridgeRequestMetadata Metadata, string OrderId, bool DemoOnly);
public sealed record BridgeCancelOrderResponse(BridgeResponseMetadata Metadata, BridgeOrderDataResult? Order, BridgeError? Error);
public sealed record BridgeClosePositionRequest(BridgeRequestMetadata Metadata, string PositionId, decimal? Quantity, bool DemoOnly);
public sealed record BridgeClosePositionResponse(BridgeResponseMetadata Metadata, BridgePositionDataResult? Position, BridgeExecutionDataResult? Execution, BridgeError? Error);
public sealed record BridgeOrdersRequest(BridgeRequestMetadata Metadata);
public sealed record BridgeOrdersResponse(BridgeResponseMetadata Metadata, ImmutableArray<BridgeOrderDataResult> Orders, BridgeError? Error);
public sealed record BridgePositionsRequest(BridgeRequestMetadata Metadata);
public sealed record BridgePositionsResponse(BridgeResponseMetadata Metadata, ImmutableArray<BridgePositionDataResult> Positions, BridgeError? Error);

internal static class BridgeValidation
{
    private static readonly Regex Sha256Pattern = new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Required(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value exceeds the {maxLength} character limit.", parameterName);
        return normalized;
    }

    public static string Message(string? value) => Required(value, nameof(value), BridgePayloadLimits.MaxMessageLength);

    public static string Sha256(string? value, string parameterName)
    {
        var normalized = Required(value, parameterName, 64);
        if (!Sha256Pattern.IsMatch(normalized)) throw new ArgumentException("A SHA-256 hexadecimal hash is required.", parameterName);
        return normalized.ToLowerInvariant();
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("The timestamp must be UTC.", parameterName);
        return value;
    }

    public static string Payload(string? value)
    {
        var normalized = value ?? throw new ArgumentNullException(nameof(value));
        if (normalized.Length == 0) throw new ArgumentException("A payload is required.", nameof(value));
        if (normalized.Length > BridgePayloadLimits.MaxPayloadBytes) throw new ArgumentException("Payload exceeds the configured limit.", nameof(value));
        return normalized;
    }

    public static ImmutableArray<T> List<T>(IEnumerable<T>? values, string parameterName)
    {
        if (values is null) throw new ArgumentNullException(parameterName);
        var result = values.ToImmutableArray();
        if (result.Length > BridgePayloadLimits.MaxCollectionItems) throw new ArgumentException("Collection exceeds the configured limit.", parameterName);
        return result;
    }
}
