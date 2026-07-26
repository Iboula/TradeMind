using System.Text.Json;

namespace TradeMind.Api.Contracts.Common;

/// <summary>Transport request for expert dispatch planning or execution.</summary>
public sealed record ExpertDispatchApiRequest
{
    public ExpertDispatchApiRequest(int schemaVersion, JsonElement payload)
    {
        SchemaVersion = schemaVersion;
        Payload = payload;
    }

    public int SchemaVersion { get; }
    public JsonElement Payload { get; }
}

/// <summary>Transport request for one expert analysis operation.</summary>
public sealed record ExpertAnalysisApiRequest(int SchemaVersion, JsonElement Payload);
/// <summary>Transport request for consensus construction.</summary>
public sealed record ConsensusApiRequest(int SchemaVersion, JsonElement Payload);
/// <summary>Transport request for a trading decision evaluation.</summary>
public sealed record TradingDecisionApiRequest(int SchemaVersion, JsonElement Payload);
/// <summary>Transport request for risk assessment.</summary>
public sealed record RiskApiRequest(int SchemaVersion, JsonElement Payload);
/// <summary>Transport request for trading plan generation.</summary>
public sealed record TradingPlanApiRequest(int SchemaVersion, JsonElement Payload);
/// <summary>Transport request for workspace construction.</summary>
public sealed record TradingWorkspaceApiRequest(int SchemaVersion, JsonElement Payload);
/// <summary>Transport request for a Trading Assistant question.</summary>
public sealed record TradingAssistantApiRequest(int SchemaVersion, JsonElement Payload);
/// <summary>Transport request for a deterministic paper-trading simulation.</summary>
public sealed record PaperTradingApiRequest(int SchemaVersion, JsonElement Payload);
