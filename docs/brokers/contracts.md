# Contracts

Les identifiants broker sont des valeurs validées et immuables. `BrokerConnectorDescriptor` décrit le broker, la version, le mode, les actifs, les types d’ordres, les capacités et les limites de concurrence.

`BrokerExecutionContext` porte seulement des métadonnées sûres : acteur, tenant, organisation, permissions, session et corrélation. Il ne porte ni secret, ni JWT, ni credential.

`BrokerOrderRequest` exige un compte, un instrument, un type d’ordre, une quantité, une clé d’idempotence, une corrélation et une date UTC. Les champs de plan et de risque sont des références, jamais des payloads complets.

Les réponses utilisent des statuts neutres (`Accepted`, `Rejected`, `Conflict`, `Replayed`, `Failed`) et `BrokerError` expose une catégorie stable, un message sûr, la possibilité de retry et une référence de trace.
