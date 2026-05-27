<p align="center">
  <img src="docs/logo.svg" alt="OneId" width="80" />
</p>

# OneId

**OneId** is a custom identity and licensing platform for OneDealer v2. It replaces ad-hoc credential management with a standards-compliant OAuth 2.0 / OpenID Connect foundation built on [OpenIddict](https://documentation.openiddict.com/), while implementing a domain-specific authorization model that off-the-shelf IDPs cannot support.

> **Status:** Proof of Concept — validating that OpenIddict can carry the full claim set, the authorization model works end-to-end, and IDP chaining + licensing are feasible.

---

## Table of Contents

- [What It Does](#what-it-does)
- [Architecture Overview](#architecture-overview)
- [Key Concepts](#key-concepts)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
- [Project Structure](#project-structure)
- [Running Tests](#running-tests)
- [Configuration Reference](#configuration-reference)

---

## What It Does

OneDealer v2 requires fine-grained authorization across hundreds of tenants — permissions scoped by multi-dimensional attributes (company, location, branch, make, market segment). Standard IDPs like Okta or Azure AD cannot model this natively.

OneId solves this by:

- Providing a standards-compliant OIDC server (authorization code + client credentials flows)
- Carrying the full permission + dimension claim set via token introspection
- Enforcing strict multi-tenant isolation at the data layer
- Delegating tenant-scoped user/role management to Tenant Admins
- Enforcing license seat counts at token issuance time
- Supporting IDP chaining (Okta / Azure AD federation)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                    OneDealer v2                      │
│  (consumes tokens; caches introspection 5 min)       │
└────────────────────┬────────────────────────────────┘
                     │ Bearer JWT (roles only)
                     │ Introspection → permissions + dimensions
                     ▼
┌─────────────────────────────────────────────────────┐
│                     OneId Server                     │
│                                                      │
│  ┌─────────────┐   ┌──────────────┐  ┌───────────┐  │
│  │  OpenIddict │   │  Auth Model  │  │ Licensing │  │
│  │  OIDC Core  │   │  (Resolver)  │  │  Engine   │  │
│  └─────────────┘   └──────────────┘  └───────────┘  │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  EF Core + PostgreSQL (tenant-scoped filters)  │  │
│  └────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
                     │
           ┌─────────┴──────────┐
           ▼                    ▼
    ┌─────────────┐     ┌──────────────┐
    │  React SPA  │     │  Seq + OTEL  │
    │  Admin UI   │     │  Observability│
    └─────────────┘     └──────────────┘
```

**Token model:** JWTs carry role names only. Permissions and dimensional attributes are returned by the introspection endpoint — cached by OneDealer v2 for 5 minutes (the permission change propagation window).

**Endpoints:**

| Path | Purpose |
|---|---|
| `GET /connect/authorize` | Authorization endpoint |
| `POST /connect/token` | Token issuance |
| `POST /connect/introspect` | Token introspection (permissions + dimensions) |
| `GET /connect/userinfo` | User info |

---

## Key Concepts

### Permissions

String-identified rights in dot-notation: `module.resource.action`

```
crm.invoice.create
erp.pricing.discount.approve
```

Defined globally in `Permissions.cs` (static catalog, never inline literals). Seeded at migration time. Assigned to Roles per Tenant — never globally.

### Roles

Named bundles of Permissions, always scoped to a Tenant. A user acquires permissions through their Group memberships → Roles → Permissions chain.

### Role Sets

Named collections of Roles for bulk assignment via Groups — composition only, no inheritance. A Role Set expands to its constituent Roles during permission resolution.

### Permission Resolution

Conflict rule: **DENY is terminal.**

1. Expand Role Sets → Roles
2. Union all Role permissions
3. Apply user-level overrides (explicit ALLOW/DENY with reason + optional expiry)
4. Apply dimensional attribute filters (axis mismatch = implicit deny)

### User-Level Overrides

Individual users can have explicit `ALLOW` or `DENY` overrides on specific permissions, with a mandatory reason and optional expiry. All overrides are audit-logged. A `DENY` override short-circuits the full resolution chain.

### Dimensions (5-Axis)

Users are assigned values on up to five axes:

| Axis | Purpose |
|---|---|
| Company | Legal entity filter |
| Location | Geographic scope |
| Branch | Dealer branch scope |
| Make | Vehicle brand scope |
| MarketSegment | Customer segment scope |

Dimensions are tenant reference lists (normalized values). They are assigned directly to users, emitted in introspection responses, and consumed by OneDealer v2 as row-level query filters. A user can hold multiple values per axis.

### Multi-Tenancy

Every tenant-scoped entity carries a `tenant_id` and a `deleted_at` (soft deletes only — no hard deletes). EF Core global query filters enforce isolation automatically. The `ITenantContext` scoped service is populated from the JWT `tid` claim.

The Internal Admin context bypasses the tenant filter and is protected by architectural enforcement tests.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / C# 13, ASP.NET Core |
| OIDC Server | OpenIddict 7.5 |
| Database | PostgreSQL 16, EF Core 10 (Npgsql) |
| Frontend | React 19, TypeScript 6, Vite 8 |
| UI Components | Radix UI, Tailwind CSS 4, shadcn/ui |
| State / Data | TanStack Query 5, TanStack Table 8, Zustand 5 |
| Routing | React Router 7 |
| Logging | Serilog → OpenTelemetry Collector → Seq |
| Tracing | OpenTelemetry 1.x, OTLP exporter |
| MFA | OTP.NET (TOTP) |
| Auth hashing | Argon2id (via ASP.NET Core Identity) |
| Containerization | Docker, Docker Compose |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 1. Start Infrastructure

```bash
docker compose up -d postgres seq otel-collector
```

This starts:
- **PostgreSQL 16** on port `5432`
- **Seq** (log UI) on port `5341` → [http://localhost:5341](http://localhost:5341)
- **OpenTelemetry Collector** on ports `4317` / `4318`

### 2. Apply Database Migrations

```bash
cd src/OneId.Server
dotnet ef database update
```

### 3. Run the Backend

```bash
cd src/OneId.Server
dotnet run
```

Server starts on [http://localhost:8080](http://localhost:8080).

### 4. Run the Frontend

```bash
cd src/OneId.Web
npm install
npm run dev
```

Frontend starts on [http://localhost:5173](http://localhost:5173).

### Run Everything via Docker

```bash
docker compose up
```

---

## Project Structure

```
OneId/
├── src/
│   ├── OneId.Server/          # ASP.NET Core backend (OpenIddict, EF Core, domain logic)
│   └── OneId.Web/             # React + TypeScript admin SPA
├── tests/
│   ├── OneId.Server.UnitTests/
│   └── OneId.Server.IntegrationTests/
├── samples/
│   └── OneId.SampleClient/    # Example OAuth 2.0 client app
├── docker-compose.yml
├── OneId.slnx
└── Directory.Build.props      # Central .NET version and warning configuration
```

### Backend (`src/OneId.Server`)

Key areas within the server project:

| Path | Contents |
|---|---|
| `Domain/` | Core domain entities (User, Tenant, Role, Permission, Group, Dimension) |
| `Application/` | Use cases, command/query handlers |
| `Infrastructure/` | EF Core context, repositories, OpenIddict stores |
| `Api/` | Controllers + minimal API endpoints |
| `Permissions.cs` | Global permission catalog (static class) |
| `keys/` | Dev RS256 signing key (stable across restarts) |

---

## Running Tests

```bash
# Unit tests
dotnet test tests/OneId.Server.UnitTests

# Integration tests (requires running PostgreSQL)
dotnet test tests/OneId.Server.IntegrationTests
```

Integration tests enforce architectural boundaries — notably that `InternalAdminContext` cannot be reached from tenant-scoped code paths.

---

## Configuration Reference

Key settings in `appsettings.json` / environment variables:

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:Default` | _(required)_ | PostgreSQL connection string |
| `OpenIddict:AccessTokenLifetimeMinutes` | `15` | Access token lifetime |
| `OpenIddict:RefreshTokenSlidingExpiryHours` | `8` | Sliding refresh token window |
| `Frontend:BaseUrl` | _(required)_ | React SPA origin for CORS + redirect URIs |

For local development, configure via `appsettings.Development.json` or Docker Compose environment variables (see `docker-compose.yml` for examples).

---

## Performance Targets (POC)

| Operation | Target |
|---|---|
| Token issuance (p95) | ≤ 500 ms |
| Introspection (p95) | ≤ 50 ms |
| Claim set scale | 50+ permissions + 5-axis dimensions |
| Revocation | Immediate (IDP-side); consumer cache expires within 5 min |
