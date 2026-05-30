# sharingbridge-user-service

> Authentication, Google Sign-In, donor presets (Node.js MVP)

## Status

**Shipped:** `POST /v1/auth/google` (web coordinator + mobile donor roles), JWT mint/verify, donor presets APIs, **PostgreSQL** via `DATABASE_URL` (required).

**Doc map:** [AGENT_HANDOFF.md](https://github.com/sharingbridge/sharingbridge/blob/main/development/AGENT_HANDOFF.md) § Documentation map.

## Run locally

```bash
npm install
npm test
npm start
```

- Health: `GET http://localhost:8081/health`
- Copy `.env.example` → `.env` (see [configuration/google-auth-setup.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/google-auth-setup.md))

### Endpoints

| Method | Path | Notes |
|--------|------|--------|
| POST | `/v1/auth/google` | `{ "id_token", "client_type": "web" \| "mobile" }` |
| POST | `/v1/auth/token` | Dev only when `ALLOW_DEV_TOKEN_MINT=true` |
| GET/PUT | `/v1/users/:userId/donor-presets` | Bearer JWT |
| POST | `/v1/users/:userId/donor-presets/delete-item` | Single preset delete |

Coordinators: seed `user_roles` in Postgres — [coordinator-seed.sql](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/coordinator-seed.sql) · [database.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/database.md).

## Deploy (Render)

Deploy **before** integration-service. [configuration/backend-render.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/backend-render.md). Blueprint: `render.yaml`.

Set `GOOGLE_CLIENT_ID_WEB`, `WEB_CORS_ORIGINS`, `AUTH_TOKEN_SECRET`; `ALLOW_DEV_TOKEN_MINT=false` on Render.

## License

MIT — see [LICENSE](LICENSE).

Part of [SharingBridge](https://github.com/sharingbridge/sharingbridge).
