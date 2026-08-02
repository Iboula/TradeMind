using TradeMind.Identity.Domain.Permissions;

namespace TradeMind.Identity.Domain.Roles;

public static class RolePermissionMapping
{
    private static readonly IReadOnlyDictionary<string, PermissionSet> Map = Build();

    public static PermissionSet GetPermissions(IEnumerable<string> roles)
    {
        var permissions = roles.Where(role => role is not null)
            .SelectMany(role => Map.TryGetValue(role.Trim(), out var set) ? (IEnumerable<Permission>)set.Values : Array.Empty<Permission>())
            .Distinct()
            .ToArray();
        return new PermissionSet(permissions);
    }

    private static IReadOnlyDictionary<string, PermissionSet> Build() => new Dictionary<string, PermissionSet>(StringComparer.Ordinal)
    {
        [TradeMindRoles.Viewer] = Set(TradeMindPermissions.SystemReadVersion, TradeMindPermissions.ExecutionSessionsRead, TradeMindPermissions.BrokersRead, TradeMindPermissions.BrokersReadAccounts),
        [TradeMindRoles.Analyst] = Set(TradeMindPermissions.MarketContextBuild, TradeMindPermissions.ExpertsDispatch, TradeMindPermissions.ExpertsAnalyze,
            TradeMindPermissions.ConsensusBuild, TradeMindPermissions.TradingDecisionsEvaluate, TradeMindPermissions.TradingPlansGenerate,
            TradeMindPermissions.TradingWorkspaceBuild, TradeMindPermissions.TradingAssistantAsk, TradeMindPermissions.ExecutionSessionsCreate,
            TradeMindPermissions.ExecutionSessionsRead, TradeMindPermissions.ExecutionSessionsSearch),
        [TradeMindRoles.Trader] = Set(TradeMindPermissions.MarketContextBuild, TradeMindPermissions.ExpertsDispatch, TradeMindPermissions.ExpertsAnalyze,
            TradeMindPermissions.ConsensusBuild, TradeMindPermissions.TradingDecisionsEvaluate, TradeMindPermissions.TradingPlansGenerate,
            TradeMindPermissions.TradingWorkspaceBuild, TradeMindPermissions.TradingAssistantAsk, TradeMindPermissions.ExecutionSessionsCreate,
            TradeMindPermissions.ExecutionSessionsRead, TradeMindPermissions.ExecutionSessionsSearch, TradeMindPermissions.PaperTradingSimulate,
            TradeMindPermissions.BrokersRead, TradeMindPermissions.BrokersReadAccounts, TradeMindPermissions.BrokersReadOrders, TradeMindPermissions.BrokersReadPositions,
            TradeMindPermissions.BrokersExecuteSimulation, TradeMindPermissions.BrokersExecuteDemo, TradeMindPermissions.BrokersModifyOrders, TradeMindPermissions.BrokersCancelOrders, TradeMindPermissions.BrokersClosePositions),
        [TradeMindRoles.RiskManager] = Set(TradeMindPermissions.RiskEvaluate, TradeMindPermissions.TradingPlansGenerate, TradeMindPermissions.ExecutionSessionsRead, TradeMindPermissions.BrokersRead, TradeMindPermissions.BrokersReadAccounts, TradeMindPermissions.BrokersReadOrders, TradeMindPermissions.BrokersReadPositions, TradeMindPermissions.BrokersReconcile),
        [TradeMindRoles.Auditor] = Set(TradeMindPermissions.ExecutionSessionsRead, TradeMindPermissions.ExecutionSessionsSearch, TradeMindPermissions.ExecutionSessionsReplay,
            TradeMindPermissions.ExecutionSessionsReadAudit, TradeMindPermissions.AdministrationReadAudit, TradeMindPermissions.ObservabilityReadTelemetry),
        [TradeMindRoles.OrganizationAdministrator] = Set(TradeMindPermissions.AdministrationManageOrganizations, TradeMindPermissions.AdministrationManageUsers,
            TradeMindPermissions.ApiKeysCreate, TradeMindPermissions.ApiKeysRead, TradeMindPermissions.ApiKeysRevoke, TradeMindPermissions.ApiKeysRotate,
            TradeMindPermissions.BrokersRead, TradeMindPermissions.BrokersManageConnectors),
        [TradeMindRoles.PlatformAdministrator] = new PermissionSet(TradeMindPermissions.All),
        [TradeMindRoles.ServiceAccount] = Set(TradeMindPermissions.SystemReadVersion)
    };

    private static PermissionSet Set(params string[] values) => new(values.Select(value => new Permission(value)));
}
