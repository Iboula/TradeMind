# Authorization

Authorization uses policy-based permissions. Endpoints declare a named
`TradeMind.Permission.*` policy. The policy handler evaluates the immutable
current actor with the application `PermissionEvaluator`; it does not inspect
controllers, database rows or broker state.

Permissions are grouped by capability: market context, expert analysis,
consensus, trading decisions, risk, plans, workspace, assistant, paper
trading, execution sessions, API keys and administration. Roles are only
bundles defined by `RolePermissionMapping`. Unknown roles and unknown claims do
not grant access.

The access decision is made before the application operation. A denied
permission is a 403 ProblemDetails response; an unauthenticated request is a
401 ProblemDetails response. API-key reads and mutations additionally verify
organization and tenant ownership. An object outside the caller's scope is
reported as not found by application contracts rather than leaked through a
cross-tenant detail.
