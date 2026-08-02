# In-memory Connector

`InMemoryBrokerConnector` est une implémentation de référence sans réseau. Il expose des comptes et instruments configurables, accepte Market/Limit/Stop selon les capacités, produit des identifiants stables, remplit les Market à un prix déterministe et garde les ordres/positions en mémoire.

Les scénarios de rejet sont injectés dans `InMemoryBrokerState`. Aucune latence artificielle basée sur `Task.Delay` n’est utilisée. Le connecteur ne fournit ni credentials, ni streaming, ni mode Demo/Live.

Paper Trading reste séparé : Paper Trading rejoue des ticks et calcule un PnL ; ce connecteur teste les sémantiques d’acceptation, de fill, d’annulation et de réconciliation.
