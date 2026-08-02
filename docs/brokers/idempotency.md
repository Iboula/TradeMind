# Idempotency

La clé stockée est un hash de tenant, connecteur, compte, opération et clé client. Le hash de requête couvre les champs normalisés de l’ordre. Une même clé avec le même hash rejoue le résultat ; une même clé avec un hash différent renvoie `Conflict`.

En production configurée, `PostgreSqlBrokerIdempotencyStore` coordonne les instances par clé primaire PostgreSQL. La violation de contrainte unique est traitée explicitement. Le mode in-memory est destiné aux tests et au développement local.

```mermaid
sequenceDiagram
    participant C as Command
    participant D as PostgreSQL
    participant B as Connector
    C->>D: INSERT key_hash + request_hash
    alt inserted
        C->>B: invoke once
        B-->>C: result
        C->>D: mark Completed + safe result
    else existing same hash
        D-->>C: replay result
    else existing different hash
        D-->>C: Conflict
    end
```
