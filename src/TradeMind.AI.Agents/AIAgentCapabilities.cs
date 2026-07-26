namespace TradeMind.AI.Agents;

public sealed record AIAgentCapabilities
{
    public static AIAgentCapabilities None { get; } = new();

    public AIAgentCapabilities(
        bool supportsPromptTemplates = false,
        bool supportsMemory = false,
        bool supportsKnowledge = false,
        bool supportsTools = false,
        bool supportsConversation = false,
        bool supportsStructuredOutput = false,
        bool supportsStreaming = false,
        bool supportsMultiTurn = false,
        bool supportsToolCalling = false,
        bool supportsAutonomousExecution = false)
    {
        if (supportsStreaming || supportsToolCalling || supportsAutonomousExecution)
        {
            throw new ArgumentException("Streaming, provider-native tool calling, and autonomous execution are not supported by this framework version.");
        }

        SupportsPromptTemplates = supportsPromptTemplates;
        SupportsMemory = supportsMemory;
        SupportsKnowledge = supportsKnowledge;
        SupportsTools = supportsTools;
        SupportsConversation = supportsConversation;
        SupportsStructuredOutput = supportsStructuredOutput;
        SupportsStreaming = false;
        SupportsMultiTurn = supportsMultiTurn;
        SupportsToolCalling = false;
        SupportsAutonomousExecution = false;
    }

    public bool SupportsPromptTemplates { get; }

    public bool SupportsMemory { get; }

    public bool SupportsKnowledge { get; }

    public bool SupportsTools { get; }

    public bool SupportsConversation { get; }

    public bool SupportsStructuredOutput { get; }

    public bool SupportsStreaming { get; }

    public bool SupportsMultiTurn { get; }

    public bool SupportsToolCalling { get; }

    public bool SupportsAutonomousExecution { get; }

    public bool Includes(AIAgentCapabilities required) =>
        (!required.SupportsPromptTemplates || SupportsPromptTemplates)
        && (!required.SupportsMemory || SupportsMemory)
        && (!required.SupportsKnowledge || SupportsKnowledge)
        && (!required.SupportsTools || SupportsTools)
        && (!required.SupportsConversation || SupportsConversation)
        && (!required.SupportsStructuredOutput || SupportsStructuredOutput)
        && (!required.SupportsStreaming || SupportsStreaming)
        && (!required.SupportsMultiTurn || SupportsMultiTurn)
        && (!required.SupportsToolCalling || SupportsToolCalling)
        && (!required.SupportsAutonomousExecution || SupportsAutonomousExecution);
}

public enum AIAgentAvailability
{
    Enabled,
    Disabled,
    DevelopmentOnly
}

public enum AIAgentFailureMode
{
    FailClosed,
    ContinueWithReducedCapabilities
}

public enum AIAgentVersionSelection
{
    Exact,
    Latest,
    LatestStable
}

public enum AIAgentExecutionState
{
    Created,
    Resolving,
    Authorizing,
    Mapping,
    Executing,
    Completed,
    Failed,
    Cancelled,
    TimedOut
}
