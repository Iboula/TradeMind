# Capabilities

Les capacités sont un ensemble immuable de flags. Une opération est refusée avant l’appel au connecteur si le flag correspondant manque.

| Opération | Capacité |
| --- | --- |
| Marché | `SubmitMarketOrders` |
| Limit | `SubmitLimitOrders` |
| Stop | `SubmitStopOrders` |
| Modification | `ModifyOrders` |
| Annulation | `CancelOrders` |
| Fermeture | `ClosePositions` |
| Lecture | `ReadAccounts`, `ReadInstruments`, `ReadOrders`, `ReadPositions` |
| Contrôle | `Reconciliation`, `IdempotentClientOrderIds` |

Le registre publie les descriptors dans l’ordre lexical des `ConnectorId`. Les doublons sont rejetés au démarrage.
