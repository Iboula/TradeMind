# Architecture Diagrams

The diagrams below describe the current runtime shape and the direction of
data through the platform. They are intentionally technology-aware at the
boundary and technology-neutral at the core.

## System context

```mermaid
flowchart TB
    Trader[Trader or analyst]
    Api[TradeMind API]
    Modules[Modular monolith modules]
    PostgreSQL[(PostgreSQL + pgvector)]
    Docker[Docker Compose]
    Testcontainers[Testcontainers]
    Providers[External AI providers through abstractions]

    Trader --> Api
    Api --> Modules
    Modules --> PostgreSQL
    Docker --> PostgreSQL
    Testcontainers --> PostgreSQL
    Modules -. optional provider adapter .-> Providers
```

## Module flow

```mermaid
flowchart LR
    Market[Market Connectors]
    Context[Market Context]
    Agents[Expert Agents]
    Dispatch[Expert Dispatcher]
    Consensus[Consensus Engine]
    Decisions[Trading Decision Engine]
    Risk[Risk Engine]
    Plans[Trading Plan Generator]
    Workspace[Trading Workspace]
    Assistant[Trading Assistant]
    Paper[Paper Trading]

    Market --> Context
    Context --> Agents
    Agents --> Dispatch
    Dispatch --> Consensus
    Consensus --> Decisions
    Decisions --> Risk
    Risk --> Plans
    Plans --> Workspace
    Workspace --> Assistant
    Workspace --> Paper
    Plans --> Paper
```

## Clean Architecture boundary

```mermaid
flowchart TB
    subgraph Core[Core and contracts]
        Domain[Domain rules]
        Ports[Application ports]
    end
    subgraph Adapters[Adapters]
        Ef[EF Core and PostgreSQL]
        Vector[pgvector]
        Ai[AI provider adapters]
        DockerAdapter[Docker and Testcontainers]
    end
    Host[API composition root]

    Domain --> Ports
    Ef --> Ports
    Vector --> Ef
    Ai --> Ports
    DockerAdapter --> Ef
    Host --> Ports
    Host --> Adapters
```

## CI quality flow

```mermaid
flowchart LR
    Checkout[Checkout] --> Restore[Restore]
    Restore --> DependencyCheck[Dependency rules]
    DependencyCheck --> Build[Release build]
    Build --> Tests[Tests with Coverlet]
    Tests --> Coverage[Coverage files]
    Coverage --> Report[ReportGenerator]
    Report --> Artifact[Coverage artifact]
```
