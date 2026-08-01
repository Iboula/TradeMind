# ADR-0019 — Provider-Neutral Broker Execution Abstraction

## Statut

Accepté pour Sprint 28.

## Contexte

TradeMind produit des plans et évaluations de risque indépendants de tout broker. Une intégration directe avec MT5 ou un SDK d’exchange contaminerait le domaine analytique, rendrait les tests non déterministes et créerait un chemin d’exécution difficile à sécuriser.

## Décision

Nous introduisons trois projets séparés : contrats et règles dans `TradeMind.Brokers.Domain`, ports et politiques dans `TradeMind.Brokers.Application`, puis adaptateurs dans `TradeMind.Brokers.Infrastructure`. L’accès est effectué par `IBrokerConnector`, découvert via un registre déterministe. Les capacités, l’autorisation, l’éligibilité et l’idempotence sont vérifiées avant l’appel au connecteur.

Le mode Simulation est le seul mode opérationnel livré. L’idempotence multi-instance utilise une clé primaire PostgreSQL et traite explicitement les conflits d’unicité. La réconciliation est en lecture seule. L’audit et la télémétrie sont provider-neutral côté ports.

## Conséquences

Les modules analytiques restent indépendants des brokers et les futurs adaptateurs sont remplaçables. La persistence ajoute des tables broker-neutres et une migration explicite. L’absence de connecteur réel limite volontairement la couverture des protocoles propriétaires ; cette couverture appartient à une phase ultérieure avec une revue sécurité dédiée.

## Rejeté

Nous ne livrons ni exécution Live, ni credentials, ni SDK MT5/cTrader/IB/exchange, ni auto-réparation de positions, ni fusion avec le moteur Paper Trading.
