# sharingbridge-user-service

> Authentication, Google Sign-In, donor/initiator presets — **ASP.NET Core 8 (C#)**

## Status

**Current runtime:** C# / .NET 8 Minimal APIs (Docker on Render).  
HTTP contracts match the previous Node service (web + mobile clients unchanged).

| Method | Path | Notes |
|--------|------|--------|
| GET | `/health` | Render health check |
| POST | `/v1/auth/google` | `{ "id_token" \| "access_token", "client_type": "web" \| "mobile" }` → JWT |
| GET/PUT | `/v1/users/:userId/donor-presets` | Bearer JWT (`sub` must match) |
| POST | `/v1/users/:userId/donor-presets/delete-item` | Bearer JWT |
| — | `/v1/users/:userId/initiator-presets*` | Alias → `donor-presets` |

JWT: HS256, claims `sub`, `role`, `roles`, `iss`, `aud`, `iat`, `exp` — must stay compatible with integration-service and photo-service (`AUTH_TOKEN_SECRET` shared).

## Project layout

```text
src/SharingBridge.UserService/
  Program.cs                 # DI, middleware, route registration
  Endpoints/                 # HTTP handlers (Minimal APIs)
  Services/                  # Google auth, JWT, request auth helpers
  Repositories/              # IUserStore + Postgres / in-memory
  Models/                    # DTOs, roles, preset helpers
  Infrastructure/            # DB URL, pool/retry options, CORS
tools/MintDevJwt/            # Dev JWT mint (replaces old Node script)
```

## Stack placement

| Language | Service |
|----------|---------|
| **C#** | **this service** (identity + presets) — reference for `DB_POOL_*` / `DB_RETRY_*` |
| Spring Boot | notification-service next, then integration/marketplace |
| Python | AI orchestration, photo (adopt same `DB_*` names when hardening) |
| TypeScript | web dashboard |

## Run locally

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cp .env.example .env   # set DATABASE_URL, GOOGLE_CLIENT_ID_WEB, WEB_CORS_ORIGINS
# Export vars into the shell, or set them in your IDE run config.
dotnet run --project src/SharingBridge.UserService
```

Optional pool/retry env vars (`DB_POOLING`, `DB_POOL_MAX`, `DB_RETRY_MAX_ATTEMPTS`, …) — see `.env.example` and [environment-variables.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/environment-variables.md#sharingbridge-user-service). Defaults apply when unset. `GET /health` → `config.data_access` shows the effective values.

Without Postgres (unit-style local):

```bash
set USER_STORE=memory
dotnet run --project src/SharingBridge.UserService
```

Mint a local JWT (same secret as integration/mobile):

```bash
set AUTH_TOKEN_SECRET=sharingbridge-dev-secret-change-me
dotnet run --project tools/MintDevJwt -- demo-user initiator
```

- Health: `GET http://localhost:8081/health`
- Docs: [google-auth-setup.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/google-auth-setup.md)

```bash
dotnet test
```

Test layout mirrors the app:

```text
tests/SharingBridge.UserService.Tests/
  Endpoints/          # HTTP contract via WebApplicationFactory
  Services/           # TokenService
  Repositories/       # InMemoryUserStore
  Models/             # Roles, DonorPresetUtils
  Infrastructure/     # DatabaseUrl, DataAccessOptions, DbRetry
  Support/            # shared test host
```

## Deploy (Render)

`runtime: docker` — see `Dockerfile` and `render.yaml`.

Set in the dashboard: `DATABASE_URL`, `GOOGLE_CLIENT_ID_WEB`, `WEB_CORS_ORIGINS`, and align `AUTH_TOKEN_SECRET` with integration + photo.

Deploy **before** relying on integration-service auth. [backend-render.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/backend-render.md).

## License

MIT — see [LICENSE](LICENSE).

Part of [SharingBridge](https://github.com/sharingbridge/sharingbridge).
