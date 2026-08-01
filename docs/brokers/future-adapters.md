# Future Adapters

Un futur adaptateur MT5, cTrader, Interactive Brokers ou exchange devra référencer uniquement `TradeMind.Brokers.Application` et `TradeMind.Brokers.Domain`, convertir ses réponses vers les types neutres et garder son SDK dans son propre projet infrastructurel.

```mermaid
flowchart LR
    N[Neutral IBrokerConnector] --> A[Future adapter boundary]
    A --> M[MT5 SDK]
    A --> C[cTrader SDK]
    A --> I[IB API]
    A --> E[Exchange SDK]
    N -. domain never sees .-> M
```

Ces adaptateurs devront ajouter leurs propres tests contractuels, erreurs mappées, health checks, secrets et vérifications de réconciliation avant toute activation.
