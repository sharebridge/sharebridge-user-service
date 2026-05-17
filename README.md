# sharingbridge-user-service

> User authentication and profiles

## Overview

This repository contains the **User Service** - manages user authentication, profiles, and preferences for all SharingBridge users.

**Key Responsibilities:**
- 🔐 User registration and authentication (phone/email)
- 👤 User profile management (donors, seekers, admins)
- 🔑 Password reset and account recovery
- ✅ Phone/email verification (OTP)
- ⚙️ User preferences and settings
- 🌐 Language and notification preferences
- 🏷️ User role and permission management
- 📊 User activity tracking and analytics
- 🚫 Account suspension and moderation

**Technology Stack:** Node.js with NestJS or Python with FastAPI + PostgreSQL

For overall project context, see the [main SharingBridge repository](https://github.com/sharingbridge/sharingbridge).

## Repository Status

🚧 **Status:** Initial Setup  
📅 **Date:** January 9, 2026

## Getting Started

```bash
npm install
npm test
npm start
```

- Health: `GET http://localhost:8081/health`
- Mint token: `POST http://localhost:8081/v1/auth/token` body `{"user_id":"demo-user"}`

Copy `.env.example` to `.env` (loaded on `npm start` via dotenv). Set `WEB_CORS_ORIGINS` when using `sharingbridge-web-app` locally.

## Deploy (Render)

Deploy **first**. See [configuration/backend-render.md](https://github.com/sharingbridge/sharingbridge/blob/main/configuration/backend-render.md). Blueprint: `render.yaml`.

## Contributing

See the [main repository's CALL_FOR_CONTRIBUTORS.md](https://github.com/sharingbridge/sharingbridge/blob/main/development/CALL_FOR_CONTRIBUTORS.md) for:
- How to contribute (technical and non-technical)
- Joining GitHub Discussions
- Submitting prompts and feature ideas

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

Part of the [SharingBridge](https://github.com/sharingbridge/sharingbridge) ecosystem
