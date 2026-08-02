# Observability

Les opérations utilisent les abstractions Sprint 27 : activités `TradeMind.Brokers.SubmitOrder`, `ModifyOrder`, `CancelOrder`, `ClosePosition`, lectures et `Reconcile`. Les métriques ajoutées couvrent opérations, erreurs, durée, ordres soumis/rejetés/remplis, positions, réconciliations et hits d’idempotence.

Les dimensions sont limitées à `module`, `operation`, `stage`, `outcome`, `connector`, `mode`, `error_category` et `asset_class`, toutes bornées. Les IDs de tenant, compte, commande, position, session et corrélation restent dans les traces sûres ou l’audit, jamais dans les labels métriques.
