# Expert Agent Dispatcher

The dispatcher turns an immutable `MarketContext` and `AgentDispatchRequest` into a deterministic `AgentDispatchPlan`.
It classifies the analysis intent, discovers candidates from the existing Expert Agent registry, selects one version,
applies authorization and compatibility policies, scores relevance, and applies Required/Preferred/Optional and budget
policies. A plan never bypasses the SDK policies and is immutable once created.

Execution is optional and is exposed through `IExpertDispatcher.DispatchAsync`. It delegates every agent call to the
existing `IExpertAgentExecutor`, executes one requirement group at a time with bounded parallelism inside a group, and
returns results in plan order. A per-agent timeout remains the SDK executor's responsibility; the dispatcher adds a
global timeout and observes every started task before returning. External cancellation is propagated to the caller.

No concrete expert agent, LLM provider, HTTP adapter, persistence, trading operation, or consensus calculation belongs in
this module. The dispatcher consumes only `MarketContext` and the public Expert Agent SDK contracts.

Register it at the composition root:

```csharp
services.AddTradeMindExpertAgentDispatcher(options =>
{
    options.MaximumParallelism = 4;
    options.DefaultGlobalTimeout = TimeSpan.FromSeconds(30);
});
```
