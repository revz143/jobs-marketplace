# Delivery tracker

| ID | Status | Acceptance evidence |
|---|---|---|
| T01 Architecture and task tracker | Done | README and this tracker record scope and boundaries. |
| T02 Docker-first scaffold | Done | `docker compose config --quiet` passes. Full startup pending Docker Desktop engine. |
| T03 Persistence and initialization | Done | `migrate` Compose service creates schema, search indexes, roles, and idempotent seeds. |
| T04 Identity and authorization | Done | Identity roles, cookie login, ownership checks, CSRF middleware, and lockout configuration implemented. |
| T05 Indexed profile search | Done | Bounded hybrid search, cursor pagination, prefix/substring behavior, trigram indexes. |
| T06 Job CRUD | Done | Customer-owned, validated, soft-deleted job endpoints. |
| T07 Offers and acceptance | Done | Contractor-owned offers; serializable transaction and parent job lock enforce one award. |
| T08 Resource limits | Done | Rate limiting, request-body/header timeouts, short database command timeout, safe exception response. |
| T09 LRU profile cache | Done | 1,000-entry, 60-second, thread-safe cache; unit tests cover cache hit and expiry. |
| T10 Next.js presentation | Done | Local demo supports login, discovery, job creation, offers, and acceptance. |
| T11 Automated verification | In progress | Static Compose validation passes; Docker engine must be started to run build/smoke test. |
| T12 GitHub handoff | Done | README, request collection, workflow, Docker configuration, and this tracker are included. |

## Remaining validation

- Start Docker Desktop, run `docker compose up --build`, then complete the walkthrough in the README.
- Run `docker compose exec api dotnet Marketplace.Api.dll --migrate` again to confirm seed idempotence.
- Record local load-test machine and outcome before making any capacity claim.
