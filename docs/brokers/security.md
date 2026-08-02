# Security

L’autorisation est séparée de la compatibilité technique. Toute commande exige un acteur authentifié, un tenant, une permission adaptée au mode et une session d’exécution.

Les métriques n’utilisent pas tenant, actor, account, order, position, session ou instrument comme dimensions. Les logs et audits ne contiennent ni credential, ni secret, ni JWT, ni payload broker brut. Les erreurs renvoyées par l’API sont des messages sûrs.

Les connecteurs secrets sont hors périmètre et devront utiliser une intégration future de gestion de secrets.
