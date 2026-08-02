# Error Model

`BrokerErrorCategory` normalise validation, autorisation, tenant, capacité, compte, instrument, marché, marge, quantité, conflit, timeout, annulation, rejet broker, transport, protocole et réconciliation.

Chaque erreur contient un code stable, un message borné, `Retryable`, `RequiresReconciliation`, un code connecteur optionnel assaini et une référence de trace. Le code connecteur ne doit jamais exposer un message propriétaire non filtré à l’API.
