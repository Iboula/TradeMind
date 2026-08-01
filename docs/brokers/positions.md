# Positions

Une position contient un compte, un instrument, un côté, une quantité, un prix moyen et des timestamps UTC. La fermeture est une commande explicite ; la réconciliation ne modifie jamais la position.

Les règles de quantité, précision, tick size, stop distance et sessions de marché proviennent exclusivement de `BrokerInstrumentSpecification`. Aucun tick value, contract size ou minimum n’est déduit du symbole.
