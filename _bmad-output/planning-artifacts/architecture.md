---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-05-21'
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-OneId-2026-05-21/prd.md
  - _bmad-output/planning-artifacts/briefs/brief-OneId-2026-05-21/brief.md
workflowType: 'architecture'
project_name: 'OneId'
user_name: 'Georgios.mathioudaki'
date: '2026-05-21'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements — 19 FRs across 6 areas:**

| Area | FRs | Architectural weight |
|---|---|---|
| Authentication (FR-1–5a) | OAuth2/OIDC flows, credentials, MFA, password reset, hybrid JWT + enriched introspection, server-side revocation | High — OpenIddict pipeline customization is the core technical risk |
| Authorization Model (FR-6–11) | Permission catalog, Roles, Role Sets, Groups, Dimensional Attribute assignment, evaluation at token issuance | High — custom domain model with no off-the-shelf equivalent |
| Multi-Tenancy (FR-12–14) | Tenant lifecycle, strict isolation, Tenant Admin delegation | High — must be enforced at data layer, not just API |
| IDP Chaining (FR-16–17) | Okta + Azure AD federation, identity mapping, claims not propagated | Medium — standard ASP.NET Core AddOpenIdConnect but needs pre-provisioning flow |
| Licensing (FR-15, 18–19) | Seat-count enforcement at token issuance, extensible model | Medium — straightforward at POC scale, extensibility constraint on data model |
| Management UI (FR-20–22) | Internal Admin + Tenant Admin consoles, audit log | Medium-High — two-tier scoped React app |

**Session-confirmed additions to authorization model (beyond PRD):**
- Role Sets as named Role bundles for bulk Group assignment (replaces PRD's RoleGroup concept)
- User-level Permission Overrides (explicit ALLOW/DENY with reason + optional expiry, audited)
- Normalized Dimensional Attribute values with per-Tenant reference lists
- String Permission IDs (`module.resource.action` dot-notation, no wildcard matching in POC)
- Effective Permissions view in User detail UI
- No role inheritance — Role Sets are the composition mechanism

**Conflict resolution:** most-specific-wins (user override > role permission > inherited via Role Set)

### Non-Functional Requirements — Critical Constraints

- Token issuance: **≤500ms p95** — large Claim Sets flagged as primary POC performance gate
- Introspection: **≤50ms p95** — cached by OneDealer v2 for 5-minute TTL; this is the accepted propagation delay for all permission/dimension changes
- Tenant data isolation at **data layer** (EF Core global query filters, not only API layer)
- RS256 token signing; credentials never logged
- Structured logging: auth success/failure, token issuance, seat enforcement, management actions

### Technical Constraints & Dependencies

- **OpenIddict** — custom claim destination wiring, enriched introspection handler, and `jti`-based server-side revocation must be validated as a POC gate
- **Database:** PostgreSQL recommended (lower cost, mature OpenIddict EF Core provider); SAP HANA Cloud under evaluation — carries EF Core provider immaturity risk. No PostgreSQL-specific features used until HANA Cloud is ruled out (no `jsonb`, no native UUID type)
- **SAP BTP deployment:** under evaluation — does not constrain POC architecture
- **OneDealer v2** has a runtime dependency on OneId's introspection endpoint
- **React** management UI — two access tiers with fully scoped data

### Scale & Complexity

- **Complexity level: Enterprise**
- **Primary domain:** Backend-heavy full-stack — OAuth2/OIDC server + fine-grained authorization domain model + React admin UI
- **Multi-tenancy:** Hundreds of Tenants, strict row-level isolation
- **Claim set scale:** Roles (JWT) + 50+ Permissions + 5-axis Dimensional Attributes (introspection) — performance constraint is real
- **Migration context:** Hundreds of existing numeric BusinessCaseIDs (OneDealer) map to string Permission identifiers post-POC; data model must accommodate this

### Cross-Cutting Concerns

1. **Tenant isolation** — every query and write scoped via EF Core global query filters
2. **Token revocation propagation** — role changes trigger `jti` invalidation at the IDP (immediate, database-backed). The 5-minute TTL is a OneDealer v2 consumer-side cache — independent of `jti` invalidation. Revoking a `jti` does NOT short-circuit the consumer cache; revocation propagates when the cache window expires. SM-6 target ("propagation in under 5 minutes") refers to the consumer cache window, not IDP revocation latency.
3. **Performance at claim resolution** — effective Permission + Dimension set computed at introspection time; ≤50ms with 50+ permissions and 5 axes
4. **Audit logging** — all significant management actions across both admin tiers
5. **OpenIddict pipeline extensibility** — custom destinations, enriched introspection, server-side revocation; must be proven end-to-end in POC
6. **IDP chaining identity mapping** — upstream (Okta/Azure AD) → OneId User by email; no auto-provisioning; failure path must be explicit
7. **Database-first (PostgreSQL)** — the system is PostgreSQL-first by design. `UseXminAsConcurrencyToken()` is used on all mutable entities; `xmin` is a PostgreSQL-specific system column and is a named migration risk if HANA Cloud is later selected. Do not treat the system as database-neutral.

## Starter Template Evaluation

### Primary Technology Domain

Backend-heavy full-stack: .NET OAuth2/OIDC server (OpenIddict) + React admin UI. Two separate projects communicating via API — no full-stack framework couples them.

### Backend — ASP.NET Core + OpenIddict

No official scaffold template exists. Bootstrapped from standard tooling:

**Initialization:**
```bash
dotnet new webapi -n OneId.Server --use-controllers
```

Then add NuGet packages:
- `OpenIddict.AspNetCore` (v7.5.0)
- `OpenIddict.EntityFrameworkCore` (v7.5.0)
- `Npgsql.EntityFrameworkCore.PostgreSQL` (PostgreSQL default; swap to HANA EF Core provider if infrastructure decision changes)

Reference patterns from [openiddict/openiddict-samples](https://github.com/openiddict/openiddict-samples) for Authorization Code Flow + Client Credentials.

**Runtime:** .NET 9

### Frontend — Vite + React + TypeScript

**Initialization:**
```bash
npm create vite@latest OneId.Web -- --template react-ts
cd OneId.Web && npx shadcn@latest init
```

**Stack:**
- Vite 6 + React 19 + TypeScript (strict mode)
- shadcn/ui — Tailwind CSS based, composable; no dependency lock-in; best-fit for admin consoles
- TanStack Query — server state / API data fetching
- TanStack Table — data-heavy views (users, permissions, audit log)
- React Router v7 — client-side routing with nested layouts supporting the two-tier admin split

No SSR — admin console is authenticated-only, no SEO requirement.

### Repository Structure

```
OneId/
├── src/
│   ├── OneId.Server/          # ASP.NET Core + OpenIddict
│   │   ├── Controllers/
│   │   ├── Domain/            # Entities, value objects
│   │   ├── Infrastructure/    # EF Core, migrations
│   │   └── Application/       # Services, handlers
│   └── OneId.Web/             # Vite + React (admin UI)
│       └── src/
│           ├── features/      # feature-sliced: auth, tenants, users, permissions...
│           └── shared/        # shared UI components, API client
└── OneId.sln

```

**Architectural decisions established by this stack:**
- TypeScript throughout frontend (strict)
- Tailwind CSS design system via shadcn/ui
- Feature-sliced frontend structure maps to the two admin tiers (Internal Admin, Tenant Admin)
- EF Core as the only ORM — database swap (PostgreSQL ↔ HANA Cloud) requires only provider package change
- Project initialization is the first implementation story

## Core Architectural Decisions

### Already Decided (Carried from PRD + Session)

OpenIddict 7.5.0 · .NET 9 · PostgreSQL + EF Core + Npgsql · RS256 JWT · TOTP MFA · 5-min introspection cache TTL · Role Sets (no inheritance) · User-level Permission Overrides · String permission IDs (dot-notation) · Normalized dimension values · EF Core global query filters for tenant isolation · Vite + React 19 + TypeScript + shadcn/ui + TanStack Query/Table + React Router v7

### Data Architecture

| Decision | Choice | Rationale |
|---|---|---|
| EF Core approach | Code-first migrations (migration bundles for CI) | Standard for greenfield; deployable without EF tooling at runtime |
| Entity IDs | `Guid` throughout | Consistent with OpenIddict; avoids integer type issues if database changes to HANA Cloud |
| Data validation | FluentValidation | Complex cross-field rules; clean separation from entity models |
| Server-side caching | `IMemoryCache` (POC) → Redis (production) | Caches resolved permission+dimension sets per user at introspection time; in-process sufficient for single-instance POC |
| Audit log storage | Dedicated `AuditLog` table in same PostgreSQL DB | Queryable alongside operational data; separate store is production hardening post-POC |

### Authentication & Security

| Decision | Choice | Rationale |
|---|---|---|
| Password hashing | Argon2id (ASP.NET Core Identity built-in) | Stronger than bcrypt; no extra packages on .NET 9 |
| Admin UI token storage | Full SPA — tokens in JS memory | Authorization Code Flow + PKCE directly from React; tokens never written to localStorage or cookies; refresh token rotation for renewal |
| CORS | Explicit allowlist — admin UI origin only | No wildcard origins from day 1 |
| Rate limiting | ASP.NET Core built-in `AddRateLimiter` | Sufficient for POC; no extra dependencies |
| Token signing key storage | Dev: file-based stable key (`signing-key.pem`, git-ignored) | Must survive app restarts — `DevSigningKeyStabilityTest` enforces this. Production key storage (Key Vault or equivalent) and rotation procedure are post-POC. |

### API & Communication

| Decision | Choice | Rationale |
|---|---|---|
| API style | REST | OpenIddict endpoints are REST-native; management API is CRUD — no GraphQL justification |
| API documentation | Scalar (`Microsoft.AspNetCore.OpenApi`) | .NET 9 default; Swashbuckle unmaintained for .NET 9+; OpenAPI 3.1 spec |
| Error handling | Problem Details — RFC 9457 (`AddProblemDetails()`) | Built into .NET 9; consistent machine-readable errors for React UI and OneDealer v2 |

### Frontend Architecture

| Decision | Choice | Rationale |
|---|---|---|
| Client/UI state | Zustand | Lightweight; TanStack Query owns server state — Zustand handles UI-only state (active tenant context, drawer/modal state) |
| Forms | React Hook Form + Zod | RHF for performant uncontrolled forms; Zod schema doubles as API contract validation; native shadcn/ui integration |
| API client | ky | Modern fetch wrapper; TypeScript-native; lighter than Axios |

### Infrastructure & Deployment

| Decision | Choice | Rationale |
|---|---|---|
| Containerization | Docker (one Dockerfile per project) | POC standard; target-agnostic for BTP or other runtimes |
| CI/CD | GitHub Actions | No additional tooling needed for POC |
| Structured logging | Serilog → `Serilog.Sinks.OpenTelemetry` → OTEL Collector → Seq | OTEL Collector handles sampling and routing; Seq for queryable log storage; production-grade pipeline from day 1 |

### Decision Impact Analysis

**Implementation sequence (dependency order — do not reorder):**
1. Serilog + OTEL pipeline — wired in project setup story, not deferred
2. EF Core schema + global query filters — filters must be active before any data access
3. Migrations — applied after filter configuration is in place
4. `ITenantContext` middleware — must be registered before OpenIddict (provides the `tid` claim the pipeline reads)
5. OpenIddict pipeline (token issuance + introspection enrichment) — POC gate
6. DevSeeder — runs only after global query filters are active; seeded data must respect isolation
7. `IPermissionEvaluator` + integration tests — POC gate
8. `LicenseCheckHandler` — added here so its async I/O cost can be measured against the 500ms token issuance budget before management API work begins
9. Management API
10. React shell + PKCE auth flow
11. UI feature routes

**Cross-component dependencies:**
- `ICacheService` wrapping `IMemoryCache` keyed by `{entity}:{userId}:{tenantId}`; invalidated on role change and tenant suspension (FR-5a, FR-12)
- React Router v7 nested layouts map to the two admin tiers — Internal Admin and Tenant Admin routes share a common authenticated shell
- Zod schemas on the frontend mirror FluentValidation rules on the backend — duplication intentional (client UX + server enforcement)

## Implementation Patterns & Consistency Rules

### Naming Patterns

**Database (PostgreSQL + EF Core with `UseSnakeCaseNamingConvention()`)**

| Element | Convention | Example |
|---|---|---|
| Tables | snake_case, plural | `tenants`, `role_sets`, `dimension_values` |
| Columns | snake_case | `tenant_id`, `created_at`, `deleted_at` |
| Primary keys | `id uuid` | `id uuid NOT NULL` |
| Foreign keys | `{entity}_id` | `tenant_id`, `role_set_id` |
| Junction tables | `{entity_a}_{entity_b}` alphabetical | `group_role_sets`, `role_permissions` |
| Indexes | `ix_{table}_{column(s)}` | `ix_users_tenant_id_email` |
| Migrations | `{timestamp}_{PascalDescription}` | `20260521_AddRoleSetTable` |

**REST API endpoints** — plural, kebab-case. Two canonical prefixes based on caller audience — no version prefix in POC (version prefix added when a v2 surface is needed):

- `/api/internal/` — Internal Admin endpoints (cross-tenant operations; requires `InternalAdmin` role)
- `/api/tenant/` — Tenant Admin endpoints (scoped to caller's `TenantId` via `ITenantContext`; requires `TenantAdmin` role)

```
GET    /api/internal/tenants
POST   /api/internal/tenants
GET    /api/internal/tenants/{tenantId}
PATCH  /api/internal/tenants/{tenantId}
GET    /api/tenant/users
POST   /api/tenant/users
GET    /api/tenant/users/{userId}
PATCH  /api/tenant/users/{userId}
DELETE /api/tenant/users/{userId}
```
Route params: camelCase (`{tenantId}`, `{userId}`). Query params: camelCase (`?pageSize=20&pageIndex=0`). No verbs in URLs.

**C# code** — standard .NET conventions. Entity classes: singular PascalCase (`Tenant`, `RoleSet`). Services: `I{Name}Service`. Commands/queries: `{Verb}{Entity}Command` / `{Entity}Query`.

**React/TypeScript**

| Element | Convention | Example |
|---|---|---|
| Component files | PascalCase | `UserList.tsx`, `PermissionCard.tsx` |
| Feature directories | kebab-case | `user-management/`, `role-sets/` |
| Hook files | `use` prefix, camelCase | `usePermissions.ts` |
| Types/schemas | `types.ts`, `schemas.ts` per feature | `features/users/types.ts` |
| API layer | `api.ts` per feature | `features/users/api.ts` |

---

### Structure Patterns

**Backend feature slice**
```
Application/
  Tenants/
    Commands/CreateTenantCommand.cs
    Queries/GetTenantQuery.cs
    TenantService.cs / ITenantService.cs
  Internal/                      ← InternalAdminContext injectable here only
    ...
Domain/
  Entities/Tenant.cs
Infrastructure/
  Persistence/
    AppDbContext.cs
    Migrations/
    Configurations/TenantConfiguration.cs   ← IEntityTypeConfiguration<T>, one per entity
```

Each entity has its own `IEntityTypeConfiguration<T>` in `Configurations/`. Never configure entities inline in `OnModelCreating`.

**Frontend feature slice**
```
src/features/
  users/
    api.ts           ← all TanStack Query hooks
    components/
      UserList.tsx
      UserList.test.tsx   ← co-located
      UserForm.tsx
    types.ts         ← inferred from Zod schemas
    schemas.ts       ← Zod schemas (source of truth)
    index.ts         ← barrel export (public surface only)
src/lib/
  api-client.ts      ← single ky instance with global beforeError hook
```

---

### Format Patterns

**API success responses**
```json
// Single resource — direct, no envelope
{ "id": "...", "name": "Athens Branch", "tenantId": "..." }

// Paginated collection — always this shape
{ "items": [...], "totalCount": 142, "pageIndex": 0, "pageSize": 20 }
```
`pageIndex` is zero-based. Default `pageSize` = 25, max = 100. Requests exceeding max return `400`.

**API error responses** — Problem Details (RFC 9457) always
```json
{
  "type": "https://oneid.onedealer.com/errors/validation",
  "title": "Validation failed",
  "status": 422,
  "errors": { "name": ["Name is required."] }
}
```

**Timestamps** — ISO 8601 UTC strings (`"2026-05-21T14:30:00Z"`) everywhere. `created_at` and `updated_at` on every entity, set by EF Core interceptor. **JSON field naming** — camelCase (ASP.NET Core default).

---

### Process Patterns

**Soft-delete** — All tenant-scoped entities carry `deleted_at timestamptz NULL`. Global query filter: `x.DeletedAt == null && x.TenantId == tenantId`. Hard deletes are forbidden on tenant-scoped data.

**Optimistic concurrency** — All mutable entities use `UseXminAsConcurrencyToken()`. Services catch `DbUpdateConcurrencyException` → return `409 Conflict` (Problem Details). No silent last-write-wins.

**Tenant isolation** — `ITenantContext` scoped service populated from JWT claim. Global query filters on all tenant-scoped entities. `InternalAdminContext` bypasses filters — injectable only in `Application/Internal/` namespace, enforced by ArchUnit test/analyzer. Build failure on cross-namespace injection.

**Permission resolution** — `IPermissionEvaluator` owns the full computation. Evaluation order: (1) Role Set expansion → (2) Role permissions union → (3) User-level ALLOW/DENY overrides → (4) Dimensional Attribute filters. **Conflict rule: DENY at any level is terminal** — a DENY override short-circuits evaluation and no subsequent step can grant access. Dimensional Attribute mismatches are treated as implicit denies. No other service computes permission sets. Has its own integration test suite.

**Permission Override expiry** — Enforced at read time via DB filter (`ExpiresAt IS NULL OR ExpiresAt > NOW()`). No background sweeper in v1. Expired records retained (audit trail).

**Audit log** — `audit_log` table present from migration 1: `id uuid`, `entity_type varchar(100)`, `entity_id uuid`, `action varchar(50)`, `actor_user_id uuid`, `tenant_id uuid NULL`, `changed_at timestamptz`, `payload jsonb`. All authorization mutations write an audit record in the same DB transaction — never async, never fire-and-forget.

**Permission catalog seeding** — Permission identifiers are version-controlled in `/Infrastructure/Persistence/Seeds/PermissionCatalog.cs`. Application code references permission IDs only via constants in a `Permissions` static class — never inline string literals.

**Cache abstraction** — All cache access via `ICacheService` wrapping `IMemoryCache` for POC. Cache keys: `{entity}:{userId}:{tenantId}`. Explicit invalidation on authorization mutations. First staging deploy triggers swap to Redis (`IDistributedCache`) — this is a gate, not a suggestion.

**FluentValidation pipeline** — All `Command`/`Query` objects validated via `IValidator<T>` registered as pipeline behavior. Invalid input → `400` Problem Details with `errors` map. No inline validation in service methods.

**ky global error interceptor** — Single `ky` instance in `lib/api-client.ts`. `beforeError` hook: `401` → clear session state (Zustand) + redirect to `/login`; `403` with `type: "tenant-suspended"` → redirect to `/suspended` page; other `403` → propagate as Problem Details to query hook. Components never inspect HTTP status codes.

**`useHasPermission` contract** — Signature: `useHasPermission(permissionId: string): { permitted: boolean; isLoading: boolean }`. Components gate on `!isLoading && permitted`. During load: neutral skeleton — never a flickering binary.

**Active tenant context** — URL is the source of truth: `/tenants/:tenantId/…`. Zustand mirrors this for reactive access but never owns it. Direct URL navigation, new tabs, refresh, and re-authentication all restore correct context from URL.

**Mutation feedback** — Pending: spinner overlay. Success: durable inline confirmation with propagation note ("Changes effective within 5 minutes"). Failure: Problem Details → `setError` (validation) or persistent dismissable toast (other). Optimistic rollback: explicit message "Change could not be saved — [reason]. Your previous value has been restored."

**Admin tier visual differentiation** — When Internal Admin operates within a tenant context: persistent banner "Operating as Internal Admin within [Tenant Name]". Write/destructive actions include a confirmation dialog naming the tenant. Tenant Admin sessions show no banner. This is a required pattern.

**Suspended tenant mid-session** — `403` with `type: "tenant-suspended"` intercepted globally by `beforeError` hook → redirect to `/suspended` page with contact-admin message. Disabled controls alone are not acceptable.

---

### All Implementation Agents MUST

1. `UseSnakeCaseNamingConvention()` — never add `[Column]` overrides unless there's a specific conflict
2. `IEntityTypeConfiguration<T>` per entity — never configure in `OnModelCreating` directly
3. All tenant-scoped entities carry `deleted_at` — no hard deletes
4. `UseXminAsConcurrencyToken()` on all mutable entities
5. Problem Details for all API errors — no custom error shapes
6. `TenantId` from `ITenantContext` only — never as a method parameter
7. `InternalAdminContext` only under `Application/Internal/` — ArchUnit enforced
8. Permission resolution through `IPermissionEvaluator` only — no ad-hoc computation
9. Audit log written in same transaction as authorization mutations
10. Permission IDs via `Permissions` static class — never inline string literals
11. Zod schema first, TypeScript types inferred — never duplicate a schema as a type
12. TanStack Query hooks in `features/{name}/api.ts` — never call `ky` from a component
13. `useHasPermission` returns `{ permitted, isLoading }` — gate on `!isLoading && permitted`
14. Active tenant context encoded in URL — Zustand mirrors, never owns
15. Admin tier banner shown when Internal Admin operates within a tenant context
16. Token signing key must be file-based and stable across restarts in dev — `DevSigningKeyStabilityTest` enforces this; key rotation and production storage are post-POC

## Project Structure & Boundaries

### Requirements → Structure Mapping

| FR Group | Lives in |
|---|---|
| FR-1–5a Authentication + OpenIddict pipeline | `Infrastructure/OpenIddict/`, `Controllers/` |
| FR-6 Permission catalog | `Application/Permissions/`, `features/permissions/` |
| FR-7 Roles | `Application/Roles/`, `features/roles/` |
| FR-8 Role Sets | `Application/RoleSets/`, `features/role-sets/` |
| FR-9 Groups | `Application/Groups/`, `features/groups/` |
| FR-10 Dimensional Attributes | `Application/Dimensions/`, `features/dimensions/` |
| FR-11 Permission evaluation | `Application/Permissions/PermissionEvaluator.cs` (single owner) |
| FR-12–14 Multi-tenancy | `Application/Tenants/`, `Application/Common/ITenantContext.cs` |
| FR-15, 18–19 Licensing | `Application/Licenses/`, `features/licenses/` |
| FR-16–17 IDP Chaining | `Infrastructure/Federation/` |
| FR-20 Internal Admin console | `routes/internal/`, `Application/Internal/` |
| FR-21 Tenant Admin console | `routes/tenant/` |
| FR-22 Audit log | `Application/Audit/`, `features/audit-log/` |

### Assembly Seams (current: single assembly; future split path)

```
OneId.Domain          ← entities, value objects, domain services, domain events
OneId.Application     ← use cases, interfaces, IPermissionEvaluator
OneId.Infrastructure  ← EF Core, OpenIddict, Caching, Federation, Logging
OneId.Server          ← host only: DI wiring, Controllers, Middleware
OneId.Contracts       ← claim schemas, API DTOs, event schemas (no dependencies)
```

Design namespaces as if this split exists — actual separation is a rename not a rewrite when needed.

### Complete Project Directory Structure

```
OneId/
├── OneId.sln
├── .github/
│   └── workflows/ci.yml
├── docker-compose.yml            ← local dev: server + postgres + otel-collector + seq
│
├── src/
│   ├── OneId.Server/
│   │   ├── OneId.Server.csproj
│   │   ├── Program.cs            ← composition root: DI, OpenIddict, middleware
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Dockerfile
│   │   │
│   │   ├── Controllers/
│   │   │   ├── TenantsController.cs
│   │   │   ├── UsersController.cs
│   │   │   ├── GroupsController.cs
│   │   │   ├── RolesController.cs
│   │   │   ├── RoleSetsController.cs
│   │   │   ├── PermissionsController.cs
│   │   │   ├── DimensionsController.cs
│   │   │   ├── LicensesController.cs
│   │   │   ├── AuditLogController.cs
│   │   │   └── Internal/
│   │   │       ├── InternalTenantsController.cs
│   │   │       └── InternalPermissionsController.cs
│   │   │
│   │   ├── Domain/
│   │   │   ├── Entities/
│   │   │   │   ├── Tenant.cs               ← Tenant.Suspend(), Tenant.Reinstate()
│   │   │   │   ├── User.cs                 ← User.Deactivate(), User.Activate()
│   │   │   │   ├── Group.cs
│   │   │   │   ├── UserGroup.cs            ← junction
│   │   │   │   ├── Role.cs
│   │   │   │   ├── RoleSet.cs
│   │   │   │   ├── RoleSetRole.cs          ← junction
│   │   │   │   ├── GroupRoleSet.cs         ← junction
│   │   │   │   ├── Permission.cs
│   │   │   │   ├── RolePermission.cs       ← junction
│   │   │   │   ├── UserPermissionOverride.cs
│   │   │   │   ├── DimensionValue.cs       ← tenant reference list
│   │   │   │   ├── UserDimensionAssignment.cs  ← normalised: one row per axis+value
│   │   │   │   ├── License.cs
│   │   │   │   ├── IdpConfiguration.cs     ← Okta/AzureAD per tenant
│   │   │   │   └── AuditLog.cs
│   │   │   ├── Services/
│   │   │   │   └── IPermissionEvaluator.cs ← interface lives in Domain (pure logic contract)
│   │   │   ├── Enums/
│   │   │   │   ├── DimensionAxis.cs
│   │   │   │   ├── PermissionOverrideType.cs
│   │   │   │   └── LicenseModel.cs
│   │   │   └── Events/
│   │   │       ├── RoleAssignmentChangedEvent.cs
│   │   │       ├── TenantSuspendedEvent.cs
│   │   │       └── UserDeactivatedEvent.cs
│   │   │
│   │   ├── Application/
│   │   │   ├── Common/
│   │   │   │   ├── ITenantContext.cs
│   │   │   │   ├── TenantContext.cs
│   │   │   │   ├── InternalAdminContext.cs
│   │   │   │   ├── ICacheService.cs
│   │   │   │   ├── Permissions.cs          ← static class: all permission ID constants
│   │   │   │   ├── Exceptions/
│   │   │   │   │   ├── NotFoundException.cs
│   │   │   │   │   ├── ForbiddenException.cs
│   │   │   │   │   └── ConflictException.cs
│   │   │   │   └── Behaviors/
│   │   │   │       ├── ValidationBehavior.cs   ← FluentValidation pipeline
│   │   │   │       └── LoggingBehavior.cs
│   │   │   ├── Tenants/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreateTenantCommand.cs
│   │   │   │   │   ├── UpdateTenantCommand.cs
│   │   │   │   │   └── SuspendTenantCommand.cs
│   │   │   │   ├── Queries/GetTenantQuery.cs
│   │   │   │   ├── ITenantService.cs
│   │   │   │   └── TenantService.cs
│   │   │   ├── Users/
│   │   │   ├── Groups/
│   │   │   ├── Roles/
│   │   │   ├── RoleSets/
│   │   │   ├── Permissions/
│   │   │   │   ├── PermissionEvaluator.cs      ← implements Domain/IPermissionEvaluator
│   │   │   │   ├── IPermissionService.cs
│   │   │   │   └── PermissionService.cs
│   │   │   ├── Dimensions/
│   │   │   ├── Licenses/
│   │   │   ├── Audit/
│   │   │   │   ├── IAuditService.cs
│   │   │   │   └── AuditService.cs
│   │   │   ├── Auth/
│   │   │   │   ├── MfaService.cs
│   │   │   │   ├── PasswordResetService.cs
│   │   │   │   └── FederationCallbackService.cs ← what to do after federation succeeds
│   │   │   └── Internal/                        ← InternalAdminContext injectable here ONLY
│   │   │       ├── InternalTenantService.cs
│   │   │       └── InternalPermissionService.cs
│   │   │
│   │   └── Infrastructure/
│   │       ├── Persistence/
│   │       │   ├── AppDbContext.cs
│   │       │   ├── Migrations/
│   │       │   ├── Configurations/             ← IEntityTypeConfiguration<T> per entity
│   │       │   │   ├── TenantConfiguration.cs
│   │       │   │   ├── UserConfiguration.cs
│   │       │   │   └── ... (one per entity)
│   │       │   ├── Seeds/
│   │       │   │   ├── PermissionCatalog.cs    ← version-controlled; synced with Permissions.cs
│   │       │   │   └── DevSeeder.cs            ← dev tenant + admin user + OpenIddict test client
│   │       │   └── Interceptors/
│   │       │       ├── TimestampInterceptor.cs ← sets created_at / updated_at
│   │       │       └── SoftDeleteInterceptor.cs ← sets deleted_at, blocks hard deletes
│   │       ├── OpenIddict/
│   │       │   ├── TokenPipelineExtensions.cs
│   │       │   ├── ClaimDestinations.cs
│   │       │   ├── IntrospectionEnricher.cs
│   │       │   ├── RevocationHandler.cs
│   │       │   └── LicenseCheckHandler.cs    ← FR-18 seat-count enforcement; runs after authentication, before token issuance; async DB read — budget against 500ms issuance p95
│   │       ├── DomainEvents/
│   │       │   └── DomainEventDispatcher.cs    ← dispatches Domain/Events via MediatR
│   │       ├── Caching/
│   │       │   └── CacheService.cs             ← ICacheService impl; IMemoryCache → Redis swap
│   │       ├── Middleware/
│   │       │   └── ExceptionHandlingMiddleware.cs ← Problem Details from domain exceptions
│   │       ├── Logging/
│   │       │   └── SerilogConfiguration.cs
│   │       └── Federation/
│   │           ├── OktaFederationHandler.cs
│   │           └── AzureAdFederationHandler.cs
│   │
│   └── OneId.Web/
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── .env.example
│       ├── index.html
│       ├── Dockerfile
│       └── src/
│           ├── main.tsx
│           ├── App.tsx
│           │
│           ├── lib/
│           │   ├── api-client.ts        ← single ky instance + beforeError hook
│           │   ├── auth.ts              ← PKCE flow, token memory storage, refresh rotation
│           │   └── utils.ts
│           │
│           ├── store/
│           │   └── tenant-store.ts      ← Zustand: mirrors tenantId from URL
│           │
│           ├── routes/
│           │   ├── index.tsx
│           │   ├── _authenticated.tsx   ← token guard shell
│           │   ├── login.tsx
│           │   ├── suspended.tsx        ← mid-session tenant suspension landing
│           │   ├── error.tsx            ← root error boundary
│           │   │
│           │   ├── internal/            ← Internal Admin
│           │   │   ├── _layout.tsx      ← GlobalNav + TenantSwitcher + AdminTierBanner
│           │   │   ├── error.tsx        ← tier-level error boundary
│           │   │   ├── index.tsx        ← Internal Admin dashboard
│           │   │   ├── tenants/
│           │   │   │   ├── index.tsx
│           │   │   │   ├── new.tsx      ← tenant creation (UJ-1)
│           │   │   │   └── $tenantId/
│           │   │   │       ├── _layout.tsx   ← "Operating within [Tenant]" banner + subnav
│           │   │   │       ├── error.tsx
│           │   │   │       ├── settings.tsx  ← tenant config / IDP / license
│           │   │   │       ├── users/
│           │   │   │       │   ├── index.tsx
│           │   │   │       │   ├── new.tsx
│           │   │   │       │   └── $userId/
│           │   │   │       │       ├── index.tsx
│           │   │   │       │       └── permissions.tsx ← deep-linkable effective permissions
│           │   │   │       ├── groups/
│           │   │   │       │   ├── index.tsx
│           │   │   │       │   ├── new.tsx
│           │   │   │       │   └── $groupId.tsx
│           │   │   │       ├── roles/
│           │   │   │       │   ├── index.tsx
│           │   │   │       │   ├── new.tsx
│           │   │   │       │   └── $roleId.tsx
│           │   │   │       └── role-sets/
│           │   │   │           ├── index.tsx
│           │   │   │           ├── new.tsx
│           │   │   │           └── $roleSetId.tsx
│           │   │   ├── permissions.tsx  ← global permission catalog
│           │   │   ├── licenses.tsx
│           │   │   ├── dimensions.tsx   ← manage dimension reference values
│           │   │   └── audit-log.tsx    ← global audit log
│           │   │
│           │   └── tenant/              ← Tenant Admin
│           │       ├── _layout.tsx      ← GlobalNav (scoped) + TenantContext header
│           │       ├── error.tsx
│           │       ├── index.tsx        ← Tenant Admin dashboard
│           │       ├── users/
│           │       │   ├── index.tsx
│           │       │   ├── new.tsx
│           │       │   └── $userId/
│           │       │       ├── index.tsx
│           │       │       └── permissions.tsx
│           │       ├── groups/
│           │       │   ├── index.tsx
│           │       │   ├── new.tsx
│           │       │   └── $groupId.tsx
│           │       ├── roles/
│           │       │   ├── index.tsx
│           │       │   ├── new.tsx
│           │       │   └── $roleId.tsx
│           │       ├── role-sets/
│           │       │   ├── index.tsx
│           │       │   ├── new.tsx
│           │       │   └── $roleSetId.tsx
│           │       └── audit-log.tsx    ← tenant-scoped audit log
│           │
│           ├── features/
│           │   ├── auth/
│           │   │   ├── api.ts
│           │   │   ├── components/
│           │   │   │   ├── LoginForm.tsx + LoginForm.test.tsx
│           │   │   │   ├── MfaForm.tsx
│           │   │   │   └── PasswordResetForm.tsx
│           │   │   ├── schemas.ts / types.ts / index.ts
│           │   ├── tenants/
│           │   ├── users/
│           │   │   ├── api.ts
│           │   │   ├── components/
│           │   │   │   ├── UserList.tsx + UserList.test.tsx
│           │   │   │   ├── UserForm.tsx
│           │   │   │   ├── EffectivePermissions.tsx
│           │   │   │   └── PermissionOverrides.tsx
│           │   │   ├── schemas.ts / types.ts / index.ts
│           │   ├── groups/
│           │   ├── roles/
│           │   ├── role-sets/
│           │   ├── permissions/
│           │   ├── dimensions/
│           │   ├── licenses/
│           │   └── audit-log/
│           │
│           ├── components/
│           │   └── shared/
│           │       ├── AdminTierBanner.tsx      ← Internal Admin context indicator
│           │       ├── GlobalNav.tsx            ← persistent sidebar; adapts per tier
│           │       ├── Breadcrumbs.tsx          ← auto-generated from route tree
│           │       ├── TenantSwitcher.tsx       ← quick-switch; preserves current sub-path
│           │       ├── CommandPalette.tsx       ← ⌘K global search/navigate (shadcn Command)
│           │       ├── DataTable.tsx            ← TanStack Table wrapper
│           │       ├── PageSkeleton.tsx
│           │       ├── EmptyState.tsx           ← first-run / zero-data states
│           │       └── MutationFeedback.tsx     ← durable success/rollback messages
│           │
│           └── hooks/
│               ├── useHasPermission.ts   ← { permitted: boolean, isLoading: boolean }
│               └── useActiveTenant.ts    ← reads URL param, syncs Zustand
│
└── tests/
    ├── OneId.Server.UnitTests/
    │   ├── Application/
    │   │   ├── Permissions/
    │   │   │   └── PermissionEvaluatorTests.cs         ← most critical; covers all eval-order cases including DENY-terminal rule
    │   │   ├── Common/Behaviors/
    │   │   │   └── ValidationBehaviorOrderTests.cs     ← FluentValidation pipeline behavior ordering
    │   │   ├── Tenants/
    │   │   ├── Licenses/
    │   │   └── ...                                     ← mirrors src/ structure as empty scaffolding
    │   └── Infrastructure/
    │       ├── SoftDeleteInterceptorTests.cs
    │       ├── ConcurrentSoftDeleteTests.cs            ← race condition between soft-delete and permission evaluation
    │       ├── DevSigningKeyStabilityTest.cs           ← signing key survives app restarts in dev
    │       └── SerilogDestructuringTests.cs            ← sensitive fields (passwords, tokens) must not appear in logs
    └── OneId.Server.IntegrationTests/
        ├── Helpers/
        │   ├── WebApplicationFactory.cs                ← Testcontainers PG + Respawn + seeded client
        │   └── TestTokenFactory.cs                     ← claims contract: tid (tenant), sub (userId), scope, seat_count, roles[] — all required fields must be present
        ├── OpenIddict/
        │   ├── TokenIssuanceTests.cs                   ← POC gate: hybrid JWT correctness, ≤500ms (measured at IDP endpoint, request receipt → response sent)
        │   ├── IntrospectionTests.cs                   ← POC gate: enriched response, ≤50ms
        │   └── SeatCountEnforcementTests.cs            ← SM-4: seat limit reached → 403; measures LicenseCheckHandler cost against 500ms budget
        ├── TenantIsolationTests.cs                     ← POC gate: cross-tenant → 403 (must include adversarial tests: Tenant B token on Tenant A endpoints returns 403 not 404 or 500)
        ├── PermissionCatalogSyncTests.cs               ← asserts every Permissions.cs constant has a DB seed row
        └── Architecture/
            ├── InternalBoundaryTests.cs                ← InternalAdminContext namespace enforcement
            ├── LayerDependencyTests.cs                 ← Domain has no Infrastructure deps
            └── PersistenceRuleTests.cs                 ← no inline OnModelCreating, no hard deletes
```

### UI Manageability — Navigation Design

The admin UI has deep nesting (Internal Admin → Tenant → Users → User → Permissions). The following shared components keep it navigable regardless of depth:

**`GlobalNav.tsx`** — Persistent left sidebar. Adapts content per tier: Internal Admin sees all tenants + global resources; Tenant Admin sees only their tenant's resources. Collapsible on smaller screens. Always shows the current section highlighted.

**`Breadcrumbs.tsx`** — Auto-generated from the React Router v7 route tree. Always visible below the page header. Every segment is a live link. Example: `Tenants / AutoGroup Ltd / Users / Maria Papadaki / Permissions`. Eliminates "where am I?" for deep routes.

**`TenantSwitcher.tsx`** — Available in the Internal Admin sidebar at all times. Switching tenants preserves the current sub-path where it makes sense (e.g., switching tenant while on `/users` lands on the new tenant's `/users`). Avoids the disorientation of resetting to the tenant root on every switch.

**`CommandPalette.tsx`** — `⌘K` / `Ctrl+K` global command palette (shadcn/ui `Command` component). Lets Internal Admins jump to any tenant, any user, or any resource by name without navigating the hierarchy. Critical for power users managing many tenants daily.

**`EmptyState.tsx`** — First-run states for every list view. New tenant → zero users → EmptyState with a "Create first user" CTA. Prevents blank DataTables that feel broken.

**Per-route `error.tsx`** — React Router v7 `errorElement` at each layout level. A failed query in a nested route shows an inline error within that segment, not a full-page crash. The rest of the UI remains functional.

**`AdminTierBanner.tsx`** — When Internal Admin operates inside a tenant context, a persistent top banner shows "Operating as Internal Admin within AutoGroup Ltd". All write/destructive actions include a confirmation dialog naming the tenant. Disappears completely in Tenant Admin sessions.

### Architectural Boundaries

**API Boundaries**
- `/connect/*` — OpenIddict OIDC endpoints. Not versioned. Consumed by React SPA and OneDealer v2.
- `/api/v1/*` — Management API. All require authenticated JWT. Tenant-scoped by global filter. `Internal/` routes require Internal Admin role claim.
- OneDealer v2 → `/connect/introspect` only.

**Data Boundaries**
- `AppDbContext`: global filters for `TenantId` + `DeletedAt` on all tenant-scoped entities.
- `InternalAdminContext`: bypasses tenant filter; soft-delete filter remains active.
- OpenIddict `openiddict_*` tables: managed by framework only, never accessed directly.

**Integration Points**
- Okta / Azure AD → `Infrastructure/Federation/` → `Application/Auth/FederationCallbackService.cs` → issues OneId JWT
- Serilog → OTEL Collector → Seq
- OneDealer v2 → `/connect/introspect` (runtime dependency, 5-min cache TTL)

## Architecture Validation

**Status: READY FOR IMPLEMENTATION** (with resolved gaps below)

### POC Success Metrics — Measurement Definitions

| Metric | Target | Measurement Boundary | Risk |
|---|---|---|---|
| SM-1 Token issuance latency | ≤500ms p95 | IDP endpoint: request receipt → response sent. Excludes OneDealer v2 consumer-side cache hits. | Medium — LicenseCheckHandler adds async DB read |
| SM-2 Introspection latency | ≤50ms p95 | IDP endpoint, cache-hit path | Low |
| SM-3 Federated login | End-to-end Okta or Azure AD flow completes | Requires pre-provisioned test federated user with known permissions in DevSeeder | Medium — test setup fragility |
| SM-4 Seat-count enforcement | Token issuance rejected when at limit | `SeatCountEnforcementTests.cs` | **HIGH — LicenseCheckHandler was missing from structure; now added** |
| SM-5 Tenant isolation | Cross-tenant access returns 403 | Adversarial tests in `TenantIsolationTests.cs`: Tenant B token on Tenant A endpoints must return 403, not 404 or 500 | Low with tests |
| SM-6 Revocation propagation | Permission change propagates in ≤5 min | jti invalidation is immediate (IDP-side, database-backed). Consumer cache (OneDealer v2) expires within 5 minutes. These are independent mechanisms — jti does not short-circuit the consumer cache. | Low |

### Gaps Resolved

**LicenseCheckHandler (SM-4 / FR-18)**
Added `Infrastructure/OpenIddict/LicenseCheckHandler.cs` to project structure. Placement: after authentication, before token issuance. Contains async DB read — cost must be measured against 500ms issuance p95 budget in `SeatCountEnforcementTests.cs` before SM-4 is claimed.

**Token signing key infrastructure**
Dev environment uses a file-based stable key (`signing-key.pem`, git-ignored). Key must survive app restarts — enforced by `DevSigningKeyStabilityTest.cs`. Production key storage (Key Vault or equivalent) and rotation procedure are out of POC scope.

**Dimensional attribute conflict resolution**
`IPermissionEvaluator` contract: **DENY at any level is terminal**. A user-level DENY override short-circuits the evaluation chain. Dimensional Attribute mismatches are treated as implicit denies. This is now documented in the Permission resolution process pattern.

**Build sequence (corrected)**
`ITenantContext` middleware must be registered before OpenIddict; global query filters must be active before DevSeeder runs. Corrected sequence documented in Core Architectural Decisions → Decision Impact Analysis.

**TestTokenFactory claims contract**
Required claims: `tid` (tenant ID), `sub` (user ID), `scope`, `seat_count`, `roles[]`. All integration tests that call `TestTokenFactory` must produce tokens with this full set.

**HTTPS middleware severity**
HTTPS enforcement is Critical for a multi-tenant IDP — unencrypted traffic exposes tenant credentials and tokens. Enforce from day 1, no exceptions.

**Missing test cases (added)**
- `ConcurrentSoftDeleteTests.cs` — race condition between soft-delete and permission evaluation
- `ValidationBehaviorOrderTests.cs` — FluentValidation pipeline behavior ordering
- `DevSigningKeyStabilityTest.cs` — signing key survives restarts in dev
- `SeatCountEnforcementTests.cs` — SM-4 gate; measures LicenseCheckHandler cost
- `SerilogDestructuringTests.cs` — sensitive fields must not appear in structured logs

### Open Items (post-POC)

- Token signing key rotation procedure and production storage
- Migration tooling for numeric BusinessCaseIDs → string permission identifiers
- Redis swap for `ICacheService` (triggered at first staging deploy)
- HANA Cloud infrastructure decision — carry PostgreSQL-first assumption until resolved
