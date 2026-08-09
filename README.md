# sharingbridge-user-service

> Authentication, Google Sign-In, donor/initiator presets — **ASP.NET Core 8 (C#)**

## Status

**Current runtime:** C# / .NET 8 Minimal APIs (Docker on Render).  
**Legacy:** Node.js MVP kept under [`legacy-node/`](./legacy-node/) for rollback reference only.

Same HTTP contracts as before (web + mobile clients unchanged):

| Method | Path | Notes |
|--------|------|--------|
| GET | `/health` | Render health check |
| POST | `/v1/auth/google` | `{ "id_token" \| "access_token", "client_type": "web" \| "mobile" }` → JWT |
| GET/PUT | `/v1/users/:userId/donor-presets` | Bearer JWT (`sub` must match) |
| POST | `/v1/users/:userId/donor-presets/delete-item` | Bearer JWT |
| — | `/v1/users/:userId/initiator-presets*` | Alias → `donor-presets` |

JWT: HS256, claims `sub`, `role`, `roles`, `iss`, `aud`, `iat`, `exp` — must stay compatible with integration-service and photo-service (`AUTH_TOKEN_SECRET` shared).

## Stack placement

| Language | Service |
|----------|---------|
| **C#** | **this service** (identity + presets) |
| Spring Boot | integration / marketplace (planned) |
| Python | AI orchestration, photo |
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

- Health: `GET http://localhost:8081/health`
- Docs: [google-auth-setup.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/google-auth-setup.md)

```bash
dotnet test
```

## Deploy (Render)

`runtime: docker` — see `Dockerfile` and `render.yaml`.

Set in the dashboard (same as before): `DATABASE_URL`, `GOOGLE_CLIENT_ID_WEB`, `WEB_CORS_ORIGINS`, and align `AUTH_TOKEN_SECRET` with integration + photo.

Deploy **before** relying on integration-service auth. [backend-render.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/backend-render.md).

## License

MIT — see [LICENSE](LICENSE).

Part of [SharingBridge](https://github.com/sharingbridge/sharingbridge).
