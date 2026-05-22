---
title: "PRD: OneId"
status: final
created: 2026-05-21
updated: 2026-05-21
---

# PRD: OneId

## 0. Document Purpose

This PRD is for the OneDealer engineering team and serves as the requirements baseline for the OneId proof of concept. It defines what must be built and validated before committing to a full implementation. The Glossary in §3 establishes canonical terminology; all FRs and UJs use Glossary terms verbatim. The primary input to this document is the OneId product brief (`_bmad-output/planning-artifacts/briefs/brief-OneId-2026-05-21/brief.md`).

---

## 1. Vision

OneId is a custom identity and licensing platform that replaces OneDealer's ad-hoc credential management with a standards-compliant OAuth2/OIDC foundation. It is built on OpenIddict — a .NET-native OAuth2/OIDC server framework — extended with a domain-specific authorization model, multi-tenancy, IDP federation, and an integrated licensing authority.

The core problem OneId solves is that OneDealer's authorization model — fine-grained Permissions scoped by multi-dimensional Attributes across hundreds of Tenants — cannot be mapped cleanly onto any off-the-shelf IDP without fighting that tool's data model. OneId owns this model natively: Permissions, Dimensional Attributes, and License state are first-class entities resolved at token validation time, and Tenant administrators can manage their own users within strict isolation boundaries.

This PRD scopes the proof of concept. The POC goal is feasibility validation: confirming that OpenIddict can carry the full OneDealer claim set in a standard-compliant token, that the authorization model is manageable end-to-end via a React admin console, and that IDP chaining and seat-count licensing work as designed. A successful POC is the gate to a full build.

---

## 2. Target Users

### 2.1 Personas

**Internal Admin** — A member of the OneDealer engineering or operations team. Provisions and configures Tenants, manages the global Permission catalog, and administers Licenses. Has full access to all OneId management surfaces.

**Tenant Admin** — A dealership IT manager or senior staff member. Manages Users, Groups, Roles, and Dimensional Attribute assignments within their Tenant. Cannot see or affect other Tenants. Operates through a scoped management view.

**Tenant User** — A dealership staff member (sales, service, parts, etc.). Authenticates to OneDealer v2 via OneId and receives a token carrying their Permissions, Dimensional Attributes, and License state. Does not interact with management surfaces.

### 2.2 Jobs To Be Done

- Internal Admin: Provision a new dealership Tenant and have them operational within minutes, not a support ticket.
- Internal Admin: Know exactly which Tenants are within their licensed seat count without querying a separate system.
- Tenant Admin: Control who in my dealership can do what, without needing to call OneDealer support for every change.
- Tenant User: Authenticate once and land in OneDealer v2 with the right data already scoped to my location, brand, and role.

### 2.3 Non-Users (POC)

End customers (car buyers) are explicitly excluded. External developers building on OneId as a platform are excluded. Users of systems other than OneDealer v2 are excluded.

### 2.4 Key User Journeys

**UJ-1. Internal Admin provisions a new Tenant.**
Internal Admin, authenticated to the management UI, creates a Tenant record, defines its Dimensional Attribute values, configures the Permission set and Role structure, assigns a seat-count License, and creates the initial Tenant Admin account. The Tenant Admin receives credentials and can immediately log in to their scoped management view.

**UJ-2. Tenant Admin onboards a new dealership employee.**
Tenant Admin, authenticated to the scoped management UI, creates a User, assigns them to one or more Groups, and sets their Dimensional Attribute values (e.g., Location=Athens, Branch=Sales, Make=BMW). The User can authenticate to OneDealer v2 and receives a token containing exactly the Permissions and Dimensional Attributes assigned — no more, no less.

**UJ-3. Tenant User authenticates to OneDealer v2.**
Tenant User opens OneDealer v2 and is redirected to OneId's login UI. They enter credentials, complete MFA, and are returned to OneDealer v2 with a signed JWT containing their Roles. OneDealer v2 validates the JWT locally for role-level checks. For fine-grained permission or dimensional context, it calls OneId's introspection endpoint, which validates the token (including revocation) and returns the full Claim Set. The introspection response is cached for the configured TTL.

**UJ-4. Federated Tenant User authenticates via Okta or Azure AD.**
A Tenant User whose Tenant is configured for Okta or Azure AD SSO opens OneDealer v2. OneId chains to the upstream IDP for authentication, then issues its own JWT with the user's OneId-managed Roles — regardless of what the upstream IDP provides. OneDealer v2 handles the token as in UJ-3.

---

## 3. Glossary

- **Tenant** — A dealership or dealer group using OneDealer v2; the top-level isolation boundary in OneId. All Users, Groups, Roles, Permissions, and Licenses are scoped to a Tenant. Cardinality: many Tenants per OneId instance.
- **User** — An individual with credentials managed by OneId, belonging to exactly one Tenant. Authenticated via password+MFA or via a federated upstream IDP.
- **Group** — A named collection of Users within a Tenant. The unit through which Roles are assigned to Users. A User may belong to multiple Groups.
- **Role** — A named bundle of Permissions within a Tenant. Assigned to Groups. A Group may hold multiple Roles.
- **RoleGroup** — A named collection of Roles used to organize and assign Roles in bulk. A Role may belong to multiple RoleGroups. [ASSUMPTION: RoleGroups are a management convenience layer, not a separate authorization entity — they do not appear in issued tokens.] Note: implementation documents and epics refer to this concept as **Role Set** — the terms are synonymous.
- **Permission** — A string-identified right to perform a specific operation, e.g., `crm.invoice.create` or `erp.pricing.discount.approve`. The atomic unit of authorization. Defined globally in the Permission catalog; referenced by Roles. Appears as an array claim in issued tokens.
- **Dimensional Attribute** — An attribute scoping a User's data visibility within a specific axis: Company, Location, Branch, Make, or MarketSegment. Assigned directly to Users (not via Roles). Emitted as claims in issued tokens; consumed by OneDealer v2 as row-level query filters. A User may hold multiple values per axis.
- **License** — An entitlement assigned to a Tenant controlling how many Users may actively use the system, enforced at token issuance. The POC implements seat-count licensing; the data model is extensible to concurrent and usage-based models. License state is returned in the Introspection Response.
- **IDP Chaining** — The mechanism by which OneId delegates authentication to an upstream Identity Provider (Okta or Azure AD in the POC) while retaining sole authority over Token content and Claim Set.
- **Token** — A signed JWT (RS256) issued by OneId to an authenticated User, containing: standard OIDC claims (sub, iss, aud, exp, jti) and the User's Role names. A server-side record keyed by `jti` is retained for revocation. OneDealer v2 validates the JWT signature locally for role-level checks.
- **Claim Set** — The full authorization context resolved for a User: Role names (in the Token), plus Permissions, Dimensional Attributes, and License state (returned by the introspection endpoint in addition to the Token claims).
- **Introspection Response** — The enriched payload returned by the `/connect/introspect` endpoint: validates the Token (including revocation check against the server-side record) and appends Permissions, Dimensional Attributes, and License state to the Token's claims. Cached by OneDealer v2 for a configurable TTL.
- **Internal Admin** — A OneDealer team member with full access to all OneId management surfaces across all Tenants.
- **Tenant Admin** — A User within a Tenant with delegated authority to manage that Tenant's Users, Groups, Roles, RoleGroups, and Dimensional Attribute assignments. Cannot exceed the Tenant's own Permission or Dimensional Attribute boundaries.

---

## 4. Features

### 4.1 Authentication

The authentication surface handles User identity verification and produces a signed Token. The authentication UI (login, MFA, password reset) is minimal — functional and on-brand but not the primary investment.

**Functional Requirements:**

#### FR-1: OAuth2/OIDC flows
OneId must support Authorization Code Flow (for OneDealer v2 web sessions) and Client Credentials Flow (for service-to-service). Token refresh must be supported. Realizes UJ-3, UJ-4.

**Consequences:**
- Authorization Code Flow with PKCE produces a valid `id_token` and `access_token` on successful authentication.
- Token refresh returns a new Token without re-authentication while the refresh token is valid.
- Standard OIDC discovery endpoint (`/.well-known/openid-configuration`) is present and accurate.

#### FR-2: Credential authentication
A User can authenticate with username and password. Passwords are stored hashed; plaintext is never persisted or logged.

**Consequences:**
- Failed authentication returns a generic error (no username enumeration).
- Account lockout triggers after [ASSUMPTION: 5] consecutive failures within a configurable window.

#### FR-3: Simple MFA
An authenticated User must complete a second factor before a Token is issued. [ASSUMPTION: TOTP (authenticator app) is the MFA method for the POC.]

**Consequences:**
- Token is not issued until both factors are verified.
- MFA enrollment is required on first login.

#### FR-4: Password reset
A User can reset their password via a time-limited email link without admin intervention.

**Consequences:**
- Reset link expires after [ASSUMPTION: 1 hour].
- Previous password cannot be reused immediately.

#### FR-5: Hybrid JWT issuance with enriched introspection
OneId issues a signed JWT (RS256) containing standard OIDC claims and the User's Role names. OpenIddict retains a server-side record keyed by the token's `jti` for revocation. The introspection endpoint validates the token (including revocation check) and enriches the response with Permissions, Dimensional Attributes, and License state fetched from the database. Realizes UJ-3, UJ-4.

**Consequences:**
- JWT payload contains: `sub`, `iss`, `aud`, `exp`, `iat`, `jti`, `roles` (array of Role name strings).
- JWT is signed RS256; OneDealer v2 validates signature locally without a network call for role-level checks.
- Introspection endpoint (`/connect/introspect`) returns: all JWT claims + `permissions` (array of Permission identifiers) + `dimensions` (object mapping each axis to an array of values) + `license` (`model`, `status`).
- License state and Permissions reflect the current state at introspection time, not at issuance time.
- [ASSUMPTION: JWT access token TTL is 15 minutes; refresh token is long-lived and stored server-side.]
- OneDealer v2 caches introspection responses for **5 minutes**. This is the maximum propagation delay for Permission changes, Dimensional Attribute changes, and role revocations. A revoked token returns `active:false` only after the cached response expires — this is the accepted tradeoff, not a bug.
- [NOTE FOR PM: OpenIddict requires every custom claim to have an explicit destination set (`SetDestinations(AccessToken)`) or it is silently dropped. POC must verify each expected claim appears in issued tokens.]

#### FR-5a: Server-side token revocation on role change
When a User's Role assignments change, OneId immediately invalidates that User's active tokens in the server-side store. Realizes the role-change propagation requirement.

**Consequences:**
- Role change in the management UI triggers deletion of the User's active token records by `jti`.
- The User's next introspection call (after the 5-minute cache expires) returns `active: false`; OneDealer v2 triggers a silent token refresh.
- After refresh, the new JWT reflects the updated Roles.
- Effective role-change propagation delay = 5-minute cache TTL. This is the accepted bound — not a defect.
- [ASSUMPTION: Only access tokens are revoked on role change; refresh tokens are not revoked unless the change is a security action (e.g., User suspension).]

---

### 4.2 Authorization Model

The authorization model defines how Permissions are structured, assigned, and evaluated at token issuance. This is the core domain model of OneId.

**Functional Requirements:**

#### FR-6: Permission catalog
An Internal Admin can create, read, update, and deactivate string-identified Permissions globally. A Permission has an identifier (e.g., `crm.invoice.create`) and a human-readable label.

**Consequences:**
- Identifiers are unique across the catalog.
- Deactivated Permissions are removed from all future token issuances but the record is retained.

#### FR-7: Role management
A Tenant Admin can create, read, update, and delete Roles within their Tenant. A Role references one or more Permissions from the global catalog.

**Consequences:**
- Deleting a Role removes it from all Groups within the Tenant.
- A Role cannot reference a Permission not present in the global catalog.

#### FR-8: RoleGroup management
A Tenant Admin can create, read, update, and delete RoleGroups within their Tenant. A RoleGroup contains one or more Roles and can be assigned to Groups.

**Consequences:**
- Assigning a RoleGroup to a Group grants all Roles within the RoleGroup to that Group's Users.

#### FR-9: Group management
A Tenant Admin can create, read, update, and delete Groups within their Tenant. Groups are assigned Roles and RoleGroups. Users are members of Groups.

**Consequences:**
- A User's effective Permission set is the union of all Permissions from all Roles held by all Groups the User belongs to.

#### FR-10: Dimensional Attribute assignment
A Tenant Admin can assign Dimensional Attribute values to Users within their Tenant across five axes: Company, Location, Branch, Make, MarketSegment.

**Consequences:**
- A User may hold multiple values per axis (e.g., Location=[Athens, Thessaloniki]).
- Dimensional Attributes are assigned per User, not per Role or Group.
- A Tenant Admin cannot assign Dimensional Attribute values outside those defined for their Tenant.

#### FR-11: Authorization evaluation at token issuance
When a Token is issued, OneId evaluates the User's full authorization context and populates the JWT and introspection Claim Set.

**Consequences:**
- **Roles (JWT):** array of all Role names the User holds across all Groups (including Roles inherited via RoleGroups). Duplicate Role names are deduplicated.
- **Permissions (introspection):** additive union of all Permissions from all Roles the User holds across all Groups (including Roles inherited via RoleGroups). If a Permission appears in any Role, the User holds it. User-level ALLOW/DENY overrides (with optional reason and expiry) may refine the evaluated set — a DENY override at any level is terminal and short-circuits further evaluation. Expired override records are ignored at read time but retained for audit trail.
- **Dimensional Attributes (introspection):** per axis, the union of all assigned values. A User with `Location = [Athens]` on one assignment and `Location = [Thessaloniki]` on another has `Location = [Athens, Thessaloniki]`. OR logic within each axis; AND logic across axes (a data row must match the User's values on every constrained axis to be visible).
- Evaluation completes within the token issuance latency budget (see §Cross-Cutting NFRs).
- [ASSUMPTION: Token does not embed RoleGroup names — only resolved Role names and Permission strings.]

---

### 4.3 Multi-Tenancy

Tenants are the top-level isolation boundary. No User, Group, Role, or data from one Tenant is visible to another.

**Functional Requirements:**

#### FR-12: Tenant lifecycle management
An Internal Admin can create, read, update, and suspend Tenants. A suspended Tenant's Users cannot authenticate.

**Consequences:**
- Creating a Tenant provisions an isolated namespace.
- Suspending a Tenant immediately revokes all active token records (`jti` entries) for all Users in that Tenant. Their next introspection call returns `active: false`; new token issuance is rejected until the Tenant is reinstated.

#### FR-13: Tenant isolation
All data reads and writes are scoped to the requesting User's Tenant. A Tenant Admin cannot read or modify data belonging to another Tenant.

**Consequences:**
- API and management UI enforce Tenant scope on every request.
- Cross-tenant data leakage is treated as a critical defect in POC testing.

#### FR-14: Tenant Admin delegation
An Internal Admin can designate one or more Users within a Tenant as Tenant Admins. A Tenant Admin can manage Users, Groups, Roles, RoleGroups, and Dimensional Attribute assignments within their Tenant.

**Consequences:**
- Tenant Admin cannot elevate a User's Permissions beyond what the Tenant itself holds.
- Tenant Admin cannot create or modify Licenses.

---

### 4.4 IDP Chaining (Federation)

> **Note on FR numbering:** FR-15 is defined in §4.5 (Licensing Authority). The numbering reflects the order in which requirements were finalized, not their section order. §4.4 contains FR-16 and FR-17.

OneId chains to upstream Identity Providers for authentication while retaining sole authority over Token content and Claim Set. The upstream IDP authenticates the User; OneId resolves the User's OneId identity and issues its own JWT. Claims from the upstream IDP are not propagated into the OneId token. SAML2 federation is out of scope for the POC.

**Functional Requirements:**

#### FR-16: Okta upstream federation
An Internal Admin can configure a Tenant to authenticate Users via Okta (OAuth2/OIDC). Realizes UJ-4.

**Consequences:**
- OneId acts as OIDC client to Okta via standard ASP.NET Core `AddOpenIdConnect`.
- On successful Okta callback, OneId maps the external identity to an existing OneId User record by email claim from Okta.
- Claims from Okta's token are not propagated — the issued JWT contains only OneId-managed content.
- If no matching OneId User exists for the Okta identity, authentication fails with a clear, user-facing error. Auto-provisioning is out of scope for the POC; Users must be pre-provisioned by a Tenant Admin.
- Existing Okta integrations continue to function after migration to OneId.

**Acceptance criteria:**
- A pre-provisioned User authenticates via Okta and receives a valid OneId JWT with correct Roles; introspection returns correct Permissions and Dimensional Attributes.
- A non-provisioned identity attempting Okta login receives a clear failure response; no partial User record is created.

#### FR-17: Azure AD upstream federation
An Internal Admin can configure a Tenant to authenticate Users via Azure AD (Microsoft Entra ID). Same behaviour as FR-16 for Token issuance and identity mapping. Realizes UJ-4.

**Consequences:**
- OneId acts as OIDC client to Azure AD via standard ASP.NET Core `AddOpenIdConnect`.
- On successful Azure AD callback, OneId maps the external identity to an existing OneId User record by email or UPN claim from Azure AD.
- Claims from Azure AD's token are not propagated — the issued JWT contains only OneId-managed content.
- If no matching OneId User exists, authentication fails with a clear error. Auto-provisioning is out of scope for the POC.

**Acceptance criteria:**
- A pre-provisioned User authenticates via Azure AD and receives a valid OneId JWT with correct Roles; introspection returns correct Permissions and Dimensional Attributes.
- A non-provisioned identity attempting Azure AD login receives a clear failure; no partial record is created.

---

### 4.5 Licensing Authority

OneId manages seat-count Licenses per Tenant and enforces them at token issuance. License state is embedded in issued Tokens so OneDealer v2 can gate access without runtime calls to OneId.

**Functional Requirements:**

#### FR-15: License assignment
An Internal Admin can create and update a seat-count License for a Tenant, specifying maximum active Users.

**Consequences:**
- License record stores: Tenant ID, model (`seat_count`), max seats, effective date.
- A Tenant may have at most one active License.
- Planned extension models: `concurrent` (max simultaneous active sessions) and `usage_based` (token/operation consumption). Data model must accommodate these without schema migration.

#### FR-18: Seat-count enforcement at token issuance
When issuing a Token, OneId checks whether the Tenant's active User count is within the licensed seat limit.

**Consequences:**
- If within limit: Token is issued with `license.status = "active"`.
- If limit exceeded: Token issuance is denied and the User receives a clear error referencing their Tenant Admin.
- **Active User for seat-count purposes:** any User account in the Tenant that is not explicitly deactivated. Deactivating a User in the management UI immediately frees their seat. This definition is model-specific — concurrent and usage-based models will define "active" differently.

#### FR-19: License data model extensibility
The License data model is designed to support concurrent and usage-based licensing models in a future version without requiring a schema migration.

**Consequences:**
- License record includes a `model` field (initially always `seat_count`) and a generic `parameters` object for model-specific configuration.
- No concurrent or usage-based enforcement logic is implemented in the POC.

---

### 4.6 Management UI

The management UI is the primary user-facing investment in OneId. It must expose the full authorization and licensing model in a navigable React application with two access tiers: Internal Admin and Tenant Admin.

**Functional Requirements:**

#### FR-20: Internal Admin console
An Internal Admin can manage: Tenants, global Permission catalog, Tenant License assignments, IDP federation configuration per Tenant, and Tenant Admin designation.

**Consequences:**
- All Tenants are visible and selectable.
- Internal Admin can impersonate a Tenant Admin view for support purposes [ASSUMPTION].

#### FR-21: Tenant Admin console
A Tenant Admin can manage within their Tenant: Users, Groups, Roles, RoleGroups, and Dimensional Attribute assignments. Cannot access other Tenants or modify Licenses.

**Consequences:**
- Navigation and data are scoped to the Tenant Admin's Tenant.
- Attempting to access out-of-scope resources returns a permission denied response.

#### FR-22: Audit log
All significant management actions (User create/update/delete, Role assignment change, License modification, IDP configuration change) are recorded with timestamp, actor, and action.

**Consequences:**
- Audit log is readable by Internal Admins.
- [ASSUMPTION: Audit log is read-only; no deletion in POC.]

---

## 5. Non-Goals

- End customer (car buyer) authentication or self-registration
- Integration with any system other than OneDealer v2
- Concurrent or usage-based licensing enforcement (data model only)
- Teams entity (model and role TBD; excluded from POC)
- SAML2 upstream federation (excluded from POC; to be revisited for full build)
- Auto-provisioning for federated users (configurable claim-based rules → default Tenant/Group/Role assignment; deferred post-POC)
- Permission migration tooling for existing numeric IDs (one-to-one mapping is planned; tooling is out of POC scope)
- Fine-grained UI access control within the management console (e.g., role-based management UI permissions)
- SSO for the management UI itself via an external IDP
- Production hardening: rate limiting, DDoS protection, high availability, disaster recovery

---

## 6. Cross-Cutting NFRs

**Security**
- Passwords stored using a modern adaptive hash (bcrypt or Argon2id).
- Tokens signed with RS256; signing keys rotated on a defined schedule [ASSUMPTION: quarterly in production; not required for POC].
- HTTPS enforced on all endpoints; no HTTP fallback.
- Tenant data isolation enforced at the data layer, not only at the API layer.
- Credentials and secrets are never logged.

**Performance**
- Token issuance (from credential verification to signed JWT) completes in under 500ms at p95 under POC test load. This is the primary POC performance gate — large Claim Sets may challenge this target.
- Management UI operations (CRUD) complete in under 1 second at p95.

**Introspection Performance**
- Introspection endpoint response time must be under 50ms p95 under POC test load (excludes network). Cache TTL is 5 minutes; OneDealer v2 calls introspection at most once per 5-minute window per active token.

**OpenIddict Extensibility Validation**
- POC must confirm that OpenIddict's token pipeline can be extended to produce the hybrid JWT + enriched introspection response without breaking standard OIDC compliance. Specifically: custom claim destination wiring, enriched introspection handler, and server-side `jti` revocation must all be demonstrated end-to-end before the POC is considered complete.

**Observability (POC minimum)**
- Structured logs for: authentication success/failure (with Tenant ID, no credential data), token issuance, seat-count enforcement decisions, management actions.

---

## 7. Integration and Dependencies

- **OpenIddict** — OAuth2/OIDC server framework. Issues signed JWTs with server-side `jti` records for revocation. Custom introspection handler enriches the response with Permissions, Dimensional Attributes, and License state beyond JWT claims.
- **OneDealer v2** — OIDC client; receives JWTs from OneId. Validates JWT signature locally for role-level checks. Calls introspection for fine-grained Claim Set (cached). Applies Dimensional Attributes as row-level query filters. Has a runtime dependency on OneId for introspection.
- **Okta** — External; Tenant-configurable. OneId acts as OIDC relying party via ASP.NET Core `AddOpenIdConnect`. Identity mapped to OneId User; claims not propagated.
- **Azure AD (Microsoft Entra ID)** — External; Tenant-configurable. OneId acts as OIDC relying party. Identity mapped to OneId User by email or UPN; claims not propagated.
- **React** — Management UI frontend.
- **[ASSUMPTION: PostgreSQL]** — Primary datastore for Tenants, Users, authorization model, and Licenses.

---

## 8. MVP Scope

### 8.1 In Scope (POC)

- OAuth2/OIDC: Authorization Code Flow with PKCE, Client Credentials, token refresh
- Hybrid JWT (roles) + enriched introspection (Permissions, Dimensional Attributes, License state)
- Server-side token revocation on role change
- Authentication UI: login, TOTP MFA, password reset
- Full authorization model: Users, Groups, Roles, RoleGroups, Permissions, Dimensional Attributes
- Multi-tenancy with strict Tenant isolation
- IDP chaining: Okta and Azure AD federation (FR-16, FR-17 — stretch goal, conditional on Epics 1–5 completing within schedule; see Epic 6)
- Seat-count licensing with extensible data model
- Management UI (React): Internal Admin and Tenant Admin consoles
- Audit log (read-only)

### 8.2 Out of Scope for MVP

- Teams entity [NOTE FOR PM: Revisit after POC — likely an ABAC dimension]
- Concurrent and usage-based licensing enforcement (data model only; see FR-19)
- Permission migration tooling (numeric → string identifiers)
- Auto-provisioning for federated users
- SAML2 federation
- Production hardening (see §5 Non-Goals)

---

## 9. Success Metrics

**Primary**

- **SM-1: Hybrid token correctness** — A JWT issued to a User contains the correct Role names; the introspection response for that token contains the correct Permissions, Dimensional Attributes, and License state for a User with 50+ Permissions and values across all 5 axes. JWT issuance completes within 500ms p95; introspection response within 50ms p95. Validates FR-5, FR-11.
- **SM-6: Role revocation propagation** — A role change applied in the management UI causes the affected User's next introspection call (after cache expiry) to return `active: false`, triggering a silent refresh with updated Roles. Validates FR-5a.
- **SM-2: End-to-end authorization management** — A Tenant Admin can create a User, assign Groups/Roles/Permissions/Dimensions, and the resulting Token reflects the exact configured state with no manual intervention. Validates FR-6 through FR-11, FR-21.
- **SM-3: IDP chaining works without token disruption** — A User authenticating via Okta or Azure AD receives a JWT and Introspection Response identical in Claim Set structure to a directly-authenticated User. Validates FR-16, FR-17.
- **SM-4: Seat-count enforcement** — A Tenant at its seat limit cannot issue new tokens; a Tenant under limit can. Validates FR-15, FR-18.
- **SM-5: Tenant isolation holds** — Targeted cross-tenant access attempts via API return 403; no data from Tenant A is accessible via a Tenant B session. Validates FR-13.

**Counter-metrics (do not optimize)**

- **SM-C1: Introspection cache correctness** — The 5-minute cache TTL is the accepted propagation delay. Do not reduce the cache TTL to near-zero to make SM-6 pass faster — that defeats the performance design. Revocation propagation within 5 minutes is the target; instant propagation is not. Counterbalances SM-1 and SM-6.

---

## 10. Open Questions

1. **MFA method** — TOTP assumed for POC. Email OTP or SMS as alternatives to be confirmed before full build.
2. **Token signing key management** — Static signing key acceptable for POC; rotation policy required before production.
3. **Dimensional Attribute source of truth** — Are the valid values per axis (e.g., the list of valid Locations for a Tenant) maintained in OneId, or does OneId reference a master list from OneDealer v2? This affects Tenant Admin UX (picker vs. free-form input).

---

## 11. Assumptions Index

- §4.1 FR-2: Account lockout threshold is 5 consecutive failures within a configurable window.
- §4.1 FR-3: MFA method is TOTP (authenticator app).
- §4.1 FR-4: Password reset link expires after 1 hour.
- §4.1 FR-5: JWT access token TTL is 15 minutes; refresh token is long-lived and stored server-side. Introspection cache TTL is 5 minutes.
- §4.1 FR-5a: Only access tokens are revoked on role change; refresh tokens are not revoked unless the action is a security event (User or Tenant suspension).
- §4.2 FR-11: JWT contains resolved Role names only (not RoleGroup names); introspection returns resolved Permission strings (not Role or RoleGroup names).
- §4.3 FR-12: Suspending a Tenant immediately revokes all active token records for all Users in that Tenant.
- §4.4 FR-16: Okta identity mapped to OneId User by email claim. No matching User = auth failure; auto-provisioning is out of POC scope.
- §4.4 FR-17: Azure AD identity mapped to OneId User by email or UPN claim. Same provisioning constraint as FR-16.
- §4.5 FR-18: Active User for seat-count = any non-deactivated User account. Deactivating a User immediately frees their seat.
- §4.6 FR-20: Internal Admin can impersonate a Tenant Admin view.
- §4.6 FR-22: Audit log is read-only in the POC.
- §7: Primary datastore is PostgreSQL.
