# Live Execution Safety

Le mode par défaut est `Simulation`. `AllowLiveExecution` vaut `false`, et toute configuration qui autoriserait Live doit conserver `RequireExplicitLiveConfirmation=true`.

Une exécution Live devrait réunir permission dédiée, confirmation explicite, connecteur compatible, plan non expiré, RiskAssessment approuvé, workspace non bloquant et configuration tenant. Sprint 28 ne fournit aucun connecteur Live ; `InMemoryBrokerConnector` refuse ce mode par son descriptor.

Il n’existe donc aucun chemin capable d’envoyer un ordre à MT5, cTrader, Interactive Brokers, Binance ou un autre broker réel.
