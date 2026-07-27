namespace TradeMind.Identity.Domain.Permissions;

public static class TradeMindPermissions
{
    public const string SystemReadVersion = "TradeMind.System.ReadVersion";
    public const string SystemReadHealth = "TradeMind.System.ReadHealth";
    public const string MarketContextBuild = "TradeMind.MarketContext.Build";
    public const string ExpertsDispatch = "TradeMind.Experts.Dispatch";
    public const string ExpertsAnalyze = "TradeMind.Experts.Analyze";
    public const string ConsensusBuild = "TradeMind.Consensus.Build";
    public const string TradingDecisionsEvaluate = "TradeMind.TradingDecisions.Evaluate";
    public const string RiskEvaluate = "TradeMind.Risk.Evaluate";
    public const string TradingPlansGenerate = "TradeMind.TradingPlans.Generate";
    public const string TradingWorkspaceBuild = "TradeMind.TradingWorkspace.Build";
    public const string TradingAssistantAsk = "TradeMind.TradingAssistant.Ask";
    public const string PaperTradingSimulate = "TradeMind.PaperTrading.Simulate";
    public const string ExecutionSessionsCreate = "TradeMind.ExecutionSessions.Create";
    public const string ExecutionSessionsRead = "TradeMind.ExecutionSessions.Read";
    public const string ExecutionSessionsSearch = "TradeMind.ExecutionSessions.Search";
    public const string ExecutionSessionsAdvance = "TradeMind.ExecutionSessions.Advance";
    public const string ExecutionSessionsCancel = "TradeMind.ExecutionSessions.Cancel";
    public const string ExecutionSessionsFail = "TradeMind.ExecutionSessions.Fail";
    public const string ExecutionSessionsReplay = "TradeMind.ExecutionSessions.Replay";
    public const string ExecutionSessionsReadAudit = "TradeMind.ExecutionSessions.ReadAudit";
    public const string ApiKeysCreate = "TradeMind.ApiKeys.Create";
    public const string ApiKeysRead = "TradeMind.ApiKeys.Read";
    public const string ApiKeysRevoke = "TradeMind.ApiKeys.Revoke";
    public const string ApiKeysRotate = "TradeMind.ApiKeys.Rotate";
    public const string AdministrationManageOrganizations = "TradeMind.Administration.ManageOrganizations";
    public const string AdministrationManageUsers = "TradeMind.Administration.ManageUsers";
    public const string AdministrationReadAudit = "TradeMind.Administration.ReadAudit";

    public static IReadOnlyList<Permission> All { get; } = typeof(TradeMindPermissions)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.FieldType == typeof(string))
        .Select(field => new Permission((string)field.GetValue(null)!))
        .OrderBy(permission => permission.Value, StringComparer.Ordinal)
        .ToArray();
}
