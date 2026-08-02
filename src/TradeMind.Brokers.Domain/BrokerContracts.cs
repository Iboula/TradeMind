namespace TradeMind.Brokers.Domain;

public sealed record BrokerTraceReference
{
    public BrokerTraceReference(string? correlationId, string? traceId, string? executionSessionId)
    {
        CorrelationId = BrokerValidation.Optional(correlationId, 128);
        TraceId = BrokerValidation.Optional(traceId, 128);
        ExecutionSessionId = BrokerValidation.Optional(executionSessionId, 128);
    }

    public string? CorrelationId { get; }
    public string? TraceId { get; }
    public string? ExecutionSessionId { get; }
}

public sealed record BrokerExecutionContext
{
    public BrokerExecutionContext(
        bool isAuthenticated,
        string? actorId,
        string? actorType,
        string? tenantId,
        string? organizationId,
        IEnumerable<string>? permissions,
        string? executionSessionId,
        string? correlationId,
        string? confirmationToken = null)
    {
        IsAuthenticated = isAuthenticated;
        ActorId = BrokerValidation.Optional(actorId);
        ActorType = BrokerValidation.Optional(actorType, 64);
        TenantId = BrokerValidation.Optional(tenantId);
        OrganizationId = BrokerValidation.Optional(organizationId);
        Permissions = (permissions ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        ExecutionSessionId = BrokerValidation.Optional(executionSessionId);
        CorrelationId = BrokerValidation.Optional(correlationId);
        ConfirmationToken = BrokerValidation.Optional(confirmationToken, 256);
    }

    public bool IsAuthenticated { get; }
    public string? ActorId { get; }
    public string? ActorType { get; }
    public string? TenantId { get; }
    public string? OrganizationId { get; }
    public IReadOnlyList<string> Permissions { get; }
    public string? ExecutionSessionId { get; }
    public string? CorrelationId { get; }
    public string? ConfirmationToken { get; }

    public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);
}

public sealed record BrokerAccount
{
    public BrokerAccount(
        BrokerAccountId accountId,
        BrokerConnectorId connectorId,
        string externalAccountReference,
        string name,
        string currency,
        BrokerAccountType accountType,
        BrokerEnvironment environment,
        decimal balance,
        decimal equity,
        decimal margin,
        decimal freeMargin,
        decimal? marginLevel,
        int? leverage,
        bool tradingEnabled,
        bool readOnly,
        BrokerAccountStatus status,
        DateTimeOffset updatedAtUtc,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        ConnectorId = connectorId ?? throw new ArgumentNullException(nameof(connectorId));
        ExternalAccountReference = BrokerValidation.Required(externalAccountReference, nameof(externalAccountReference), 128);
        Name = BrokerValidation.Required(name, nameof(name), 128);
        Currency = BrokerValidation.Required(currency, nameof(currency), 16);
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        Balance = balance;
        Equity = equity;
        Margin = margin;
        FreeMargin = freeMargin;
        MarginLevel = marginLevel;
        Leverage = leverage;
        TradingEnabled = tradingEnabled;
        ReadOnly = readOnly;
        Status = status;
        Environment = environment;
        UpdatedAtUtc = updatedAtUtc;
        Metadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal).AsReadOnly();
        AccountType = accountType;
    }

    public BrokerAccountId AccountId { get; }
    public BrokerConnectorId ConnectorId { get; }
    public string ExternalAccountReference { get; }
    public string Name { get; }
    public string Currency { get; }
    public BrokerAccountType AccountType { get; }
    public BrokerEnvironment Environment { get; }
    public decimal Balance { get; }
    public decimal Equity { get; }
    public decimal Margin { get; }
    public decimal FreeMargin { get; }
    public decimal? MarginLevel { get; }
    public int? Leverage { get; }
    public bool TradingEnabled { get; }
    public bool ReadOnly { get; }
    public BrokerAccountStatus Status { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}

public sealed record BrokerTradingSession
{
    public BrokerTradingSession(TimeOnly opensAtUtc, TimeOnly closesAtUtc, DayOfWeek day)
    {
        OpensAtUtc = opensAtUtc;
        ClosesAtUtc = closesAtUtc;
        Day = day;
    }

    public TimeOnly OpensAtUtc { get; }
    public TimeOnly ClosesAtUtc { get; }
    public DayOfWeek Day { get; }
}

public sealed record BrokerInstrumentSpecification
{
    public BrokerInstrumentSpecification(
        string instrument,
        string brokerSymbol,
        BrokerAssetClass assetClass,
        string baseCurrency,
        string quoteCurrency,
        int pricePrecision,
        int quantityPrecision,
        decimal tickSize,
        decimal tickValue,
        decimal contractSize,
        decimal minimumQuantity,
        decimal maximumQuantity,
        decimal quantityStep,
        decimal minimumStopDistance,
        IEnumerable<BrokerTradingSession>? tradingSessions,
        BrokerMarketStatus marketStatus,
        IEnumerable<BrokerOrderType> supportedOrderTypes,
        DateTimeOffset updatedAtUtc)
    {
        Instrument = BrokerValidation.Required(instrument, nameof(instrument), 128);
        BrokerSymbol = BrokerValidation.Required(brokerSymbol, nameof(brokerSymbol), 128);
        BaseCurrency = BrokerValidation.Required(baseCurrency, nameof(baseCurrency), 16);
        QuoteCurrency = BrokerValidation.Required(quoteCurrency, nameof(quoteCurrency), 16);
        if (pricePrecision is < 0 or > 18 || quantityPrecision is < 0 or > 18) throw new ArgumentOutOfRangeException(nameof(pricePrecision));
        if (tickSize <= 0 || tickValue <= 0 || contractSize <= 0) throw new ArgumentOutOfRangeException(nameof(tickSize));
        if (minimumQuantity <= 0 || maximumQuantity < minimumQuantity || quantityStep <= 0) throw new ArgumentOutOfRangeException(nameof(minimumQuantity));
        if (minimumStopDistance < 0) throw new ArgumentOutOfRangeException(nameof(minimumStopDistance));
        if (updatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(updatedAtUtc));
        PricePrecision = pricePrecision;
        QuantityPrecision = quantityPrecision;
        TickSize = tickSize;
        TickValue = tickValue;
        ContractSize = contractSize;
        MinimumQuantity = minimumQuantity;
        MaximumQuantity = maximumQuantity;
        QuantityStep = quantityStep;
        MinimumStopDistance = minimumStopDistance;
        TradingSessions = Array.AsReadOnly((tradingSessions ?? []).ToArray());
        MarketStatus = marketStatus;
        SupportedOrderTypes = Array.AsReadOnly((supportedOrderTypes ?? throw new ArgumentNullException(nameof(supportedOrderTypes))).Distinct().OrderBy(value => value).ToArray());
        AssetClass = assetClass;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Instrument { get; }
    public string BrokerSymbol { get; }
    public BrokerAssetClass AssetClass { get; }
    public string BaseCurrency { get; }
    public string QuoteCurrency { get; }
    public int PricePrecision { get; }
    public int QuantityPrecision { get; }
    public decimal TickSize { get; }
    public decimal TickValue { get; }
    public decimal ContractSize { get; }
    public decimal MinimumQuantity { get; }
    public decimal MaximumQuantity { get; }
    public decimal QuantityStep { get; }
    public decimal MinimumStopDistance { get; }
    public IReadOnlyList<BrokerTradingSession> TradingSessions { get; }
    public BrokerMarketStatus MarketStatus { get; }
    public IReadOnlyList<BrokerOrderType> SupportedOrderTypes { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    public bool IsQuantityValid(decimal quantity) => quantity >= MinimumQuantity && quantity <= MaximumQuantity &&
        decimal.Round((quantity - MinimumQuantity) / QuantityStep, 8) == decimal.Truncate((quantity - MinimumQuantity) / QuantityStep);
}

public sealed record BrokerOrderRequest
{
    public BrokerOrderRequest(
        BrokerExecutionId executionId,
        string executionSessionId,
        BrokerConnectorId connectorId,
        BrokerAccountId accountId,
        string clientOrderId,
        string instrument,
        BrokerOrderSide side,
        BrokerOrderType orderType,
        decimal quantity,
        decimal? requestedPrice,
        decimal? stopPrice,
        decimal? stopLoss,
        IEnumerable<decimal>? takeProfits,
        BrokerTimeInForce timeInForce,
        DateTimeOffset? expirationUtc,
        string idempotencyKey,
        string correlationId,
        string? tenantId,
        string? organizationId,
        string? actorId,
        string? tradingPlanId,
        string? riskAssessmentId,
        IReadOnlyDictionary<string, string>? metadata,
        DateTimeOffset createdAtUtc,
        int schemaVersion = 1)
    {
        ExecutionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        ExecutionSessionId = BrokerValidation.Required(executionSessionId, nameof(executionSessionId));
        ConnectorId = connectorId ?? throw new ArgumentNullException(nameof(connectorId));
        AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        ClientOrderId = BrokerValidation.Required(clientOrderId, nameof(clientOrderId), 128);
        Instrument = BrokerValidation.Required(instrument, nameof(instrument), 128);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (expirationUtc is { } expiration && expiration.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(expirationUtc));
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(createdAtUtc));
        if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (orderType == BrokerOrderType.Market && requestedPrice is not null) throw new ArgumentException("Market orders cannot have a requested price.", nameof(requestedPrice));
        ExecutionSessionId = executionSessionId.Trim();
        Side = side;
        OrderType = orderType;
        Quantity = quantity;
        RequestedPrice = requestedPrice;
        StopPrice = stopPrice;
        StopLoss = stopLoss;
        TakeProfits = Array.AsReadOnly((takeProfits ?? []).ToArray());
        TimeInForce = timeInForce;
        ExpirationUtc = expirationUtc;
        IdempotencyKey = BrokerValidation.Required(idempotencyKey, nameof(idempotencyKey), 256);
        CorrelationId = BrokerValidation.Required(correlationId, nameof(correlationId), 128);
        TenantId = BrokerValidation.Optional(tenantId);
        OrganizationId = BrokerValidation.Optional(organizationId);
        ActorId = BrokerValidation.Optional(actorId);
        TradingPlanId = BrokerValidation.Optional(tradingPlanId);
        RiskAssessmentId = BrokerValidation.Optional(riskAssessmentId);
        Metadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.Ordinal).AsReadOnly();
        CreatedAtUtc = createdAtUtc;
        SchemaVersion = schemaVersion;
    }

    public BrokerExecutionId ExecutionId { get; }
    public string ExecutionSessionId { get; }
    public BrokerConnectorId ConnectorId { get; }
    public BrokerAccountId AccountId { get; }
    public string ClientOrderId { get; }
    public string Instrument { get; }
    public BrokerOrderSide Side { get; }
    public BrokerOrderType OrderType { get; }
    public decimal Quantity { get; }
    public decimal? RequestedPrice { get; }
    public decimal? StopPrice { get; }
    public decimal? StopLoss { get; }
    public IReadOnlyList<decimal> TakeProfits { get; }
    public BrokerTimeInForce TimeInForce { get; }
    public DateTimeOffset? ExpirationUtc { get; }
    public string IdempotencyKey { get; }
    public string CorrelationId { get; }
    public string? TenantId { get; }
    public string? OrganizationId { get; }
    public string? ActorId { get; }
    public string? TradingPlanId { get; }
    public string? RiskAssessmentId { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public int SchemaVersion { get; }
}

public sealed record BrokerOrderModificationRequest(BrokerOrderId OrderId, decimal? Quantity, decimal? LimitPrice, decimal? StopPrice, DateTimeOffset? ExpirationUtc);
public sealed record BrokerOrderCancellationRequest(BrokerOrderId OrderId, string Reason);
public sealed record BrokerPositionCloseRequest(BrokerPositionId PositionId, decimal? Quantity);
public sealed record BrokerInstrumentQuery(string Instrument);
public sealed record BrokerOrderQuery(string? ClientOrderId = null, BrokerOrderStatus? Status = null);
public sealed record BrokerPositionQuery(string? Instrument = null);

public sealed record BrokerOrder
{
    public BrokerOrder(BrokerOrderId orderId, BrokerExecutionId executionId, BrokerConnectorId connectorId, BrokerAccountId accountId, string clientOrderId, string instrument, BrokerOrderSide side, BrokerOrderType orderType, decimal quantity, decimal filledQuantity, decimal? requestedPrice, decimal? averageFillPrice, BrokerOrderStatus status, BrokerTimeInForce timeInForce, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        OrderId = orderId ?? throw new ArgumentNullException(nameof(orderId));
        ExecutionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        ConnectorId = connectorId ?? throw new ArgumentNullException(nameof(connectorId));
        AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        ClientOrderId = BrokerValidation.Required(clientOrderId, nameof(clientOrderId), 128);
        Instrument = BrokerValidation.Required(instrument, nameof(instrument), 128);
        if (quantity <= 0 || filledQuantity < 0 || filledQuantity > quantity) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (createdAtUtc.Offset != TimeSpan.Zero || updatedAtUtc.Offset != TimeSpan.Zero || updatedAtUtc < createdAtUtc) throw new ArgumentException("Order timestamps are invalid.");
        FilledQuantity = filledQuantity;
        Quantity = quantity;
        RequestedPrice = requestedPrice;
        AverageFillPrice = averageFillPrice;
        Side = side;
        OrderType = orderType;
        Status = status;
        TimeInForce = timeInForce;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public BrokerOrderId OrderId { get; }
    public BrokerExecutionId ExecutionId { get; }
    public BrokerConnectorId ConnectorId { get; }
    public BrokerAccountId AccountId { get; }
    public string ClientOrderId { get; }
    public string Instrument { get; }
    public BrokerOrderSide Side { get; }
    public BrokerOrderType OrderType { get; }
    public decimal Quantity { get; }
    public decimal FilledQuantity { get; }
    public decimal? RequestedPrice { get; }
    public decimal? AverageFillPrice { get; }
    public BrokerOrderStatus Status { get; }
    public BrokerTimeInForce TimeInForce { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed record BrokerPosition
{
    public BrokerPosition(BrokerPositionId positionId, BrokerConnectorId connectorId, BrokerAccountId accountId, string instrument, BrokerOrderSide side, decimal quantity, decimal averagePrice, DateTimeOffset openedAtUtc, DateTimeOffset updatedAtUtc)
    {
        PositionId = positionId ?? throw new ArgumentNullException(nameof(positionId));
        ConnectorId = connectorId ?? throw new ArgumentNullException(nameof(connectorId));
        AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        Instrument = BrokerValidation.Required(instrument, nameof(instrument), 128);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (openedAtUtc.Offset != TimeSpan.Zero || updatedAtUtc.Offset != TimeSpan.Zero || updatedAtUtc < openedAtUtc) throw new ArgumentException("Position timestamps are invalid.");
        Side = side;
        Quantity = quantity;
        AveragePrice = averagePrice;
        OpenedAtUtc = openedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public BrokerPositionId PositionId { get; }
    public BrokerConnectorId ConnectorId { get; }
    public BrokerAccountId AccountId { get; }
    public string Instrument { get; }
    public BrokerOrderSide Side { get; }
    public decimal Quantity { get; }
    public decimal AveragePrice { get; }
    public DateTimeOffset OpenedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed record BrokerExecution
{
    public BrokerExecution(BrokerExecutionId executionId, BrokerOrderId orderId, BrokerConnectorId connectorId, BrokerAccountId accountId, BrokerOrderStatus status, decimal filledQuantity, decimal? averagePrice, DateTimeOffset occurredAtUtc, BrokerError? error = null, BrokerPositionId? positionId = null)
    {
        ExecutionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        OrderId = orderId ?? throw new ArgumentNullException(nameof(orderId));
        ConnectorId = connectorId ?? throw new ArgumentNullException(nameof(connectorId));
        AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        if (filledQuantity < 0) throw new ArgumentOutOfRangeException(nameof(filledQuantity));
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(occurredAtUtc));
        FilledQuantity = filledQuantity;
        AveragePrice = averagePrice;
        OccurredAtUtc = occurredAtUtc;
        Status = status;
        Error = error;
        PositionId = positionId;
    }

    public BrokerExecutionId ExecutionId { get; }
    public BrokerOrderId OrderId { get; }
    public BrokerConnectorId ConnectorId { get; }
    public BrokerAccountId AccountId { get; }
    public BrokerPositionId? PositionId { get; }
    public BrokerOrderStatus Status { get; }
    public decimal FilledQuantity { get; }
    public decimal? AveragePrice { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public BrokerError? Error { get; }
}

public sealed record BrokerError
{
    public BrokerError(string code, BrokerErrorCategory category, string message, bool retryable, bool requiresReconciliation, string? connectorCode, BrokerTraceReference traceReference, DateTimeOffset occurredAtUtc)
    {
        Code = BrokerValidation.Required(code, nameof(code), 64);
        Message = BrokerValidation.Required(message, nameof(message), 512);
        ConnectorCode = BrokerValidation.Optional(connectorCode, 128);
        TraceReference = traceReference ?? throw new ArgumentNullException(nameof(traceReference));
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(occurredAtUtc));
        Category = category;
        Retryable = retryable;
        RequiresReconciliation = requiresReconciliation;
        OccurredAtUtc = occurredAtUtc;
    }

    public string Code { get; }
    public BrokerErrorCategory Category { get; }
    public string Message { get; }
    public bool Retryable { get; }
    public bool RequiresReconciliation { get; }
    public string? ConnectorCode { get; }
    public BrokerTraceReference TraceReference { get; }
    public DateTimeOffset OccurredAtUtc { get; }
}

public sealed record BrokerHealth
{
    public BrokerHealth(BrokerConnectorId connectorId, BrokerHealthStatus status, string message, DateTimeOffset checkedAtUtc, TimeSpan duration)
    {
        ConnectorId = connectorId ?? throw new ArgumentNullException(nameof(connectorId));
        Message = BrokerValidation.Required(message, nameof(message), 256);
        if (checkedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(checkedAtUtc));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        Status = status;
        CheckedAtUtc = checkedAtUtc;
        Duration = duration;
    }

    public BrokerConnectorId ConnectorId { get; }
    public BrokerHealthStatus Status { get; }
    public string Message { get; }
    public DateTimeOffset CheckedAtUtc { get; }
    public TimeSpan Duration { get; }
}

public sealed record BrokerHealthResult(BrokerHealth Health, BrokerError? Error);
public sealed record BrokerOrderSubmissionResult(BrokerOrder? Order, BrokerExecution? Execution, BrokerError? Error);
public sealed record BrokerOrderModificationResult(BrokerOrder? Order, BrokerError? Error);
public sealed record BrokerOrderCancellationResult(BrokerOrder? Order, BrokerError? Error);
public sealed record BrokerPositionCloseResult(BrokerPosition? Position, BrokerExecution? Execution, BrokerError? Error);
