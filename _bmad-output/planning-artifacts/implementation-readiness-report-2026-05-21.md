---
stepsCompleted: ["step-01-document-discovery", "step-02-prd-analysis", "step-03-epic-coverage-validation", "step-04-ux-alignment", "step-05-epic-quality-review", "step-06-final-assessment"]
documentsIncluded:
  prd: _bmad-output/planning-artifacts/prds/prd-OneId-2026-05-21/prd.md
  prd_addendum: _bmad-output/planning-artifacts/prds/prd-OneId-2026-05-21/addendum.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  ux: _bmad-output/planning-artifacts/ux-design-specification.md
  epics: _bmad-output/planning-artifacts/epics.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-05-21
**Project:** OneId

---

## PRD Analysis

### Functional Requirements

FR-1: OAuth2/OIDC flows — OneId must support Authorization Code Flow with PKCE (for OneDealer v2 web sessions) and Client Credentials Flow (for service-to-service). Token refresh must be supported. OIDC discovery endpoint (/.well-known/openid-configuration) must be present and accurate.

FR-2: Credential authentication — A User can authenticate with username and password. Passwords stored hashed; plaintext never persisted or logged. Generic error on failure (no username enumeration). Account lockout after 5 consecutive failures within a configurable window.

FR-3: Simple MFA — An authenticated User must complete TOTP (authenticator app) second factor before a Token is issued. MFA enrollment required on first login. Token not issued until both factors verified.

FR-4: Password reset — A User can reset their password via a time-limited email link (expires after 1 hour) without admin intervention. Previous password cannot be reused immediately.

FR-5: Hybrid JWT issuance with enriched introspection — OneId issues a signed JWT (RS256) containing standard OIDC claims (sub, iss, aud, exp, iat, jti) and the User's Role names. OpenIddict retains a server-side record keyed by jti for revocation. Introspection endpoint (/connect/introspect) validates token (including revocation) and returns enriched response: all JWT claims + permissions (array) + dimensions (object mapping each axis to values) + license (model, status). License state and Permissions reflect current state at introspection time, not issuance. JWT TTL: 15 minutes; refresh token long-lived server-side; introspection cache TTL: 5 minutes.

FR-5a: Server-side token revocation on role change — When a User's Role assignments change, OneId immediately invalidates that User's active tokens in the server-side store (deletes jti records). User's next introspection call after 5-minute cache expiry returns active:false; OneDealer v2 triggers silent refresh. After refresh, new JWT reflects updated Roles. Only access tokens revoked on role change (not refresh tokens unless security action).

FR-6: Permission catalog — An Internal Admin can create, read, update, and deactivate string-identified Permissions globally (e.g., crm.invoice.create). Permission has unique identifier and human-readable label. Deactivated Permissions removed from future token issuances but record retained.

FR-7: Role management — A Tenant Admin can create, read, update, and delete Roles within their Tenant. A Role references one or more Permissions from the global catalog. Deleting a Role removes it from all Groups within the Tenant. A Role cannot reference a Permission not in the global catalog.

FR-8: RoleGroup management — A Tenant Admin can create, read, update, and delete RoleGroups within their Tenant. A RoleGroup contains one or more Roles and can be assigned to Groups. Assigning a RoleGroup to a Group grants all Roles within the RoleGroup to that Group's Users.

FR-9: Group management — A Tenant Admin can create, read, update, and delete Groups within their Tenant. Groups are assigned Roles and RoleGroups. Users are members of Groups. A User's effective Permission set is the union of all Permissions from all Roles held by all Groups the User belongs to.

FR-10: Dimensional Attribute assignment — A Tenant Admin can assign Dimensional Attribute values to Users across five axes: Company, Location, Branch, Make, MarketSegment. A User may hold multiple values per axis. Attributes assigned per User (not per Role or Group). A Tenant Admin cannot assign values outside those defined for their Tenant.

FR-11: Authorization evaluation at token issuance — When a Token is issued: JWT Roles = union of all Role names across all Groups (including RoleGroup-resolved Roles), deduplicated. Introspection Permissions = additive union of all Permissions from all Roles (purely additive, no conflicts). Introspection Dimensional Attributes = per axis, union of all assigned values (OR within axis, AND across axes). Evaluation within token issuance latency budget.

FR-12: Tenant lifecycle management — An Internal Admin can create, read, update, and suspend Tenants. Creating a Tenant provisions an isolated namespace. Suspending a Tenant immediately revokes all active token records (jti entries) for all Users in that Tenant; new token issuance rejected until reinstated.

FR-13: Tenant isolation — All data reads and writes scoped to requesting User's Tenant. A Tenant Admin cannot read or modify data belonging to another Tenant. API and management UI enforce Tenant scope on every request. Cross-tenant data leakage treated as critical defect.

FR-14: Tenant Admin delegation — An Internal Admin can designate one or more Users within a Tenant as Tenant Admins. Tenant Admin can manage Users, Groups, Roles, RoleGroups, and Dimensional Attribute assignments within their Tenant. Tenant Admin cannot elevate a User's Permissions beyond what the Tenant itself holds. Tenant Admin cannot create or modify Licenses.

FR-15: License assignment — An Internal Admin can create and update a seat-count License for a Tenant, specifying maximum active Users. License record stores: Tenant ID, model (seat_count), max seats, effective date. A Tenant may have at most one active License.

FR-16: Okta upstream federation — An Internal Admin can configure a Tenant to authenticate Users via Okta (OAuth2/OIDC). OneId acts as OIDC client to Okta via ASP.NET Core AddOpenIdConnect. On successful Okta callback, OneId maps external identity to existing OneId User record by email claim. Claims from Okta not propagated. No matching User = auth failure (auto-provisioning out of scope). Users must be pre-provisioned by Tenant Admin.

FR-17: Azure AD upstream federation — An Internal Admin can configure a Tenant to authenticate Users via Azure AD (Microsoft Entra ID). Same behaviour as FR-16 for Token issuance and identity mapping. Identity mapped by email or UPN claim. Claims not propagated. No matching User = auth failure.

FR-18: Seat-count enforcement at token issuance — When issuing a Token, OneId checks whether the Tenant's active User count is within the licensed seat limit. Within limit: Token issued with license.status = "active". Limit exceeded: Token issuance denied with clear error referencing Tenant Admin. Active User = any User account not explicitly deactivated. Deactivating a User immediately frees their seat.

FR-19: License data model extensibility — License data model supports concurrent and usage-based licensing models in future without schema migration. License record includes model field (initially seat_count) and generic parameters object for model-specific configuration. No concurrent or usage-based enforcement logic implemented in POC.

FR-20: Internal Admin console — An Internal Admin can manage: Tenants, global Permission catalog, Tenant License assignments, IDP federation configuration per Tenant, and Tenant Admin designation. All Tenants visible and selectable. Internal Admin can impersonate a Tenant Admin view.

FR-21: Tenant Admin console — A Tenant Admin can manage within their Tenant: Users, Groups, Roles, RoleGroups, and Dimensional Attribute assignments. Cannot access other Tenants or modify Licenses. Navigation and data scoped to Tenant Admin's Tenant. Out-of-scope resource access returns permission denied.

FR-22: Audit log — All significant management actions (User create/update/delete, Role assignment change, License modification, IDP configuration change) are recorded with timestamp, actor, and action. Readable by Internal Admins. Read-only in POC.

**Total FRs: 22** (FR-1 through FR-22, including FR-5a; note FR-15 appears in §4.5 out of numerical sequence)

---

### Non-Functional Requirements

NFR-S1: Passwords stored using a modern adaptive hash (bcrypt or Argon2id).

NFR-S2: Tokens signed with RS256; signing keys rotated on a defined schedule (quarterly in production; not required for POC).

NFR-S3: HTTPS enforced on all endpoints; no HTTP fallback.

NFR-S4: Tenant data isolation enforced at the data layer, not only at the API layer.

NFR-S5: Credentials and secrets are never logged.

NFR-P1: Token issuance (credential verification to signed JWT) completes in under 500ms at p95 under POC test load. Primary POC performance gate.

NFR-P2: Management UI operations (CRUD) complete in under 1 second at p95.

NFR-P3: Introspection endpoint response time under 50ms p95 under POC test load (excludes network). Cache TTL is 5 minutes.

NFR-OE1: POC must confirm OpenIddict token pipeline can be extended for hybrid JWT + enriched introspection without breaking standard OIDC compliance. Must demonstrate: custom claim destination wiring, enriched introspection handler, and server-side jti revocation end-to-end.

NFR-O1: Structured logs for: authentication success/failure (with Tenant ID, no credential data), token issuance, seat-count enforcement decisions, management actions.

**Total NFRs: 10**

---

### Additional Requirements / Constraints

**Technical Constraints (from Addendum):**
- Every custom claim in OpenIddict requires explicit SetDestinations(AccessToken) or it is silently dropped — no exception thrown.
- Claims from external (upstream IDP) authentication are not automatically available in OpenIddict's pipeline — must be manually read from AuthenticateResult during external sign-in callback.
- OpenIddict v3/v4 API used; v5 has breaking changes to server options API; pre-v4 community samples must not be used without review.

**Token Architecture Decision (Addendum):**
- ⚠️ **CRITICAL CONFLICT**: The PRD body (FR-5) describes a "Hybrid JWT" (JWT with roles in payload + enriched introspection for permissions/dimensions/license). The addendum explicitly states "Decision: Reference tokens chosen" where OneDealer v2 calls introspection on each request. These two descriptions are mutually contradictory. The addendum appears to supersede the PRD body but this has NOT been reconciled in the FR text. This must be resolved before implementation.

**Integration Constraints:**
- PostgreSQL as primary datastore (assumption).
- RoleGroups are management convenience only — do not appear in issued tokens.
- Teams entity excluded from POC scope.

**Assumptions requiring validation before implementation:**
1. MFA method — TOTP assumed; to be confirmed.
2. Token signing key management — static key acceptable for POC; rotation policy needed before production.
3. Dimensional Attribute source of truth — maintained in OneId or referenced from OneDealer v2?

---

### PRD Completeness Assessment

**Strengths:** The PRD is thorough, well-structured, and covers the core domain comprehensively. Requirements are numbered, traceable, and linked to user journeys. Assumptions are clearly indexed in §11.

**Gaps / Issues Identified:**
1. **FR-15 ordering anomaly** — FR-15 (License assignment) appears in §4.5 but §4.4 jumps from FR-14 to FR-16, creating a gap in the narrative sequence. Minor but could cause confusion during traceability.
2. **CRITICAL: Token architecture contradiction** — PRD body describes hybrid JWT; addendum says reference tokens chosen. Architecture and epics must be validated against one consistent position.
3. **FR-5 destination discrepancy** — FR-5 is titled "Hybrid JWT issuance" but the addendum decision supersedes this. Downstream documents may be referencing conflicting architectures.
4. **No explicit FR for User lifecycle management** — The PRD discusses Users extensively but there is no standalone FR for User create/read/update/deactivate CRUD (e.g., "Tenant Admin can create/update/deactivate Users"). FR-14 mentions Tenant Admin delegation; FR-21 references managing Users; but no FR explicitly defines User lifecycle operations with their full consequences.

---

## Epic Coverage Validation

### Coverage Matrix

| FR | PRD Requirement Summary | Epic Coverage | Status |
|---|---|---|---|
| FR-1 | OAuth2/OIDC flows (Auth Code + PKCE, Client Credentials, refresh, discovery) | Epic 2 | ✓ Covered |
| FR-2 | Credential auth, Argon2id hashing, lockout after 5 failures | Epic 2 | ✓ Covered |
| FR-3 | TOTP MFA, enrollment on first login | Epic 2 | ✓ Covered |
| FR-4 | Password reset via 1-hour email link | Epic 2 | ✓ Covered |
| FR-5 | RS256 JWT + enriched introspection (permissions, dimensions, license) | Epic 2 | ⚠️ Covered — token architecture contradiction (see below) |
| FR-5a | Server-side jti revocation on role change | Epic 2 | ✓ Covered |
| FR-6 | Global Permission catalog (Internal Admin CRUD) | Epic 4a | ✓ Covered |
| FR-7 | Role management (Tenant Admin CRUD) | Epic 4a | ✓ Covered |
| FR-8 | RoleGroup management | Epic 4a | ✓ Covered (renamed "Role Sets" in epics — terminology changed) |
| FR-9 | Group management (Tenant Admin CRUD) | Epic 4a | ✓ Covered |
| FR-10 | Dimensional Attribute assignment (5 axes, per-user) | Epic 4a | ✓ Covered |
| FR-11 | Authorization evaluation at token issuance | Epic 4b | ⚠️ Expanded — epics add DENY terminal overrides; PRD says "purely additive" |
| FR-12 | Tenant lifecycle CRUD + suspension with jti revocation | Epic 3 | ✓ Covered |
| FR-13 | Tenant data isolation (EF Core global query filters) | Epic 3 | ✓ Covered |
| FR-14 | Tenant Admin designation by Internal Admin | Epic 3 | ✓ Covered |
| FR-15 | Seat-count License creation and update | Epic 3 | ✓ Covered |
| FR-16 | Okta upstream federation | Epic 6★ | ⚠️ STRETCH GOAL — not committed POC scope |
| FR-17 | Azure AD upstream federation | Epic 6★ | ⚠️ STRETCH GOAL — not committed POC scope |
| FR-18 | Seat-count enforcement at token issuance | Epic 3 | ✓ Covered |
| FR-19 | License data model extensibility (model + parameters JSON) | Epic 3 | ✓ Covered |
| FR-20 | Internal Admin console (all management surfaces) | Epics 5a–5c | ✓ Covered |
| FR-21 | Tenant Admin console (Users, Groups, Roles, Role Sets, Dimensions) | Epics 5a–5c | ❌ PARTIAL — User lifecycle backend API (create/update/deactivate) has no backend story |
| FR-22 | Audit log (read-only, scoped by tier) | Epic 5c | ❌ PARTIAL — Audit log backend infrastructure (event writing, API endpoints) has no backend story |

**Coverage Statistics:**
- Total PRD FRs: 22 (FR-1 through FR-22, including FR-5a)
- FRs fully covered: 17
- FRs covered as stretch goal only: 2 (FR-16, FR-17)
- FRs partially covered (missing backend story): 2 (FR-21, FR-22)
- FRs covered with scope expansion vs PRD: 1 (FR-11 — DENY overrides added)
- FRs covered with token architecture contradiction: 1 (FR-5)
- **Coverage: 17/22 fully committed = 77%. 19/22 partially or stretch-covered = 86%.**

---

### Missing Requirements — Critical Gaps

#### ❌ CRITICAL: User Lifecycle Backend API (impacts FR-21, FR-18)

There is no backend story for Tenant Admin User CRUD operations. The only reference to `POST /api/tenant/users` is in Story 5c.2 (a **frontend** story — the New User stepper). No backend story exists for:

- `POST /api/tenant/users` — create a User (Tenant Admin creates a new account with initial credentials)
- `GET /api/tenant/users` — list Users in the Tenant
- `GET /api/tenant/users/{id}` — get User detail
- `PUT /api/tenant/users/{id}` — update User profile (email, name)
- `DELETE /api/tenant/users/{id}` or `POST /api/tenant/users/{id}/deactivate` — deactivate a User

**Why critical:** FR-18 states "Deactivating a User immediately frees their seat" — seat-count enforcement depends on a deactivation API existing. The seat enforcement story (Story 3.5) tests against active user counts, but with no deactivation API, the "free a seat" path is untestable.

**Recommendation:** Insert a new story between Epic 4a and Epic 5c, or add to Epic 3/4a: "User Lifecycle Management API (Tenant Admin)" covering all five endpoints above, `UseXminAsConcurrencyToken()` on the `User` entity, and the full `TenantIsolationRegressionTests.cs` extension for User queries.

---

#### ❌ CRITICAL: Audit Log Backend Infrastructure (impacts FR-22)

Epic 5c (Story 5c.5) covers the audit log **UI** — but there is no backend story for:

- `AuditLogEntry` entity + EF Core migration
- Audit event emission from all management operations (User create/update/delete, Role assignment change, License modification, IDP configuration change — as specified in FR-22)
- `GET /api/tenant/audit` endpoint (with tenant-scoped filter + pagination)
- `GET /api/internal/audit` endpoint (all tenants, with pagination)
- The `AuditLogPerformanceTest` referenced in Story 5c.5 (`AuditLogPerformanceTest` — the backend test is referenced in the UI story but its home epic is undefined)

**Why critical:** Story 5c.5's acceptance criteria reference `GET /api/tenant/audit` returning paginated data. Without a backend story defining the audit service, this endpoint doesn't exist when Story 5c.5 is implemented.

**Recommendation:** Add a new backend story in Epic 3 or early Epic 5: "Audit Log Infrastructure" covering the `AuditLogEntry` entity, audit emission service wired into management operations, and the two read endpoints with pagination.

---

### Significant Discrepancies (require clarification, not necessarily missing stories)

#### ⚠️ Token Architecture Contradiction (FR-5 / Addendum vs Epics)

- **PRD addendum** explicitly states: "Decision: Reference tokens chosen. Claim Set is not embedded in the token; it is stored server-side and returned via introspection."
- **PRD body (FR-5) and all Epics (FR-5, Story 2.4, Story 2.5, Story 4b.3)** describe a **signed JWT (RS256)** issued to the client, with enriched introspection as a supplemental call — not pure reference tokens.
- These are architecturally different: reference tokens = opaque string; hybrid JWT = signed JWT with jti server-side.
- The epics are internally consistent with the hybrid JWT model. The addendum appears to be a superseded decision that was never reconciled with the main documents.
- **Impact:** If OpenIddict is configured for reference tokens (opaque), the JWT validation path in OneDealer v2 ("validates JWT signature locally for role-level checks") breaks — reference tokens cannot be validated locally.
- **Recommendation:** Formally close this decision in the PRD — the epics' hybrid JWT model (signed JWT + server-side jti revocation + enriched introspection) is what should be built. Update or retract the addendum's "Reference tokens chosen" decision.

#### ⚠️ FR-11 Scope Expansion: DENY Overrides Not in PRD

- PRD FR-11 states: "Permissions are purely additive. No conflict resolution — Permissions are purely additive."
- Epics FR-11 adds: "DENY override at any level is terminal (short-circuits evaluation)."
- AR-11 adds the full `UserPermissionOverride` data model with ALLOW/DENY/expiry/reason.
- Story 4b.1 and Story 4b.2 implement and test this — it is fully scoped in the epics.
- **Impact:** This is a deliberate and well-scoped expansion (design decision from [[project-oneid-design-decisions]]). However the PRD FR-11 text was never updated to reflect it. Any stakeholder reading the PRD today gets an incorrect picture of the authorization model.
- **Recommendation:** Update PRD FR-11 to reflect DENY overrides and add a sentence to §4.2 describing user-level overrides. This is a documentation gap, not an implementation gap.

#### ⚠️ FR-16, FR-17 (Federation) demoted to Stretch Goal

- PRD §8.1 lists "IDP chaining: Okta and Azure AD federation" as **In Scope (POC)**.
- Epics Epic 6 is explicitly marked "★ Stretch Goal ★ — not committed POC scope."
- **Impact:** If the POC demo requires federation to be demonstrated (per the PRD success metrics SM-3 and UJ-4), Epic 6 being a stretch goal creates a scope risk.
- **Recommendation:** Align the PRD §8.1 scope statement with the epics. Either re-commit Epic 6 to POC scope, or update the PRD to move federation to §8.2 Out of Scope with a note that it is deferred.

#### ⚠️ Open Question 3 resolved in epics but not closed in PRD

- PRD Open Question 3 asks: "Are valid Dimensional Attribute values per axis maintained in OneId, or does OneId reference a master list from OneDealer v2?"
- Epics AR-12 and Story 4a.5 answer this: per-Tenant reference lists maintained in OneId (`DimensionValue` table).
- **Recommendation:** Close Open Question 3 in the PRD with the decision: "Dimension reference values are maintained per-Tenant in OneId via the `DimensionValue` table."

#### ⚠️ "RoleGroups" (PRD) vs "Role Sets" (Epics) — Terminology Inconsistency

- PRD FR-8 uses the term "RoleGroups." The epics consistently use "Role Sets" (FR-8 text updated in epics, all stories use RoleSet/Role Sets).
- The epics explicitly note this in their FR-8 text: "Role Set contains one or more Roles and can be assigned to Groups (bulk assignment mechanism; replaces RoleGroups from PRD)."
- **Recommendation:** Update PRD FR-8 heading and text to use "Role Sets" for consistency. Minor but will reduce developer confusion.

---

## UX Alignment Assessment

### UX Document Status

**Found:** `_bmad-output/planning-artifacts/ux-design-specification.md` (82,185 bytes, 2026-05-21)

Comprehensive UX specification covering: executive summary, target user personas and mental models, core experience definition, emotional design principles, UX pattern analysis (inspiring products + transferable patterns), design system foundation (shadcn/ui + Tailwind), and 22 formal UX Design Requirements (UX-DR1–22) — all mapped to epics.

---

### UX ↔ PRD Alignment

**Strongly aligned.** The UX document was authored with the PRD as its primary input and shows consistent traceability:

- Personas (Internal Admin, Tenant Admin, Tenant User) match PRD §2.1 exactly.
- Key Design Challenges map directly to PRD features: authorization model comprehension (FR-6–11), two-tier admin context (FR-20, FR-21), propagation delay honesty (FR-5 / 5-minute TTL), dimensional attribute assignment (FR-10).
- Novel UX patterns are grounded in PRD requirements: Effective Permissions panel (FR-5/11), propagation delay surfaced in the data (FR-5 introspection cache), Command Palette for Internal Admin global navigation (FR-20), URL-encoded tenant context (FR-13 isolation).
- User journeys in UX spec match PRD UJ-1 through UJ-4.

**Discrepancy vs PRD:**
- UX spec correctly uses "Role Sets" throughout; PRD still says "RoleGroups." Already flagged in Epic Coverage section.
- UX spec assumes DENY overrides and user-level overrides are in scope (design challenge #1 explicitly mentions "User-level Overrides"); PRD FR-11 text still says "purely additive." Already flagged.

---

### UX ↔ Architecture Alignment

**Strongly aligned overall.** Key compatibility checkpoints confirmed:

- **Design system**: Architecture decisions table confirms `shadcn/ui + Tailwind CSS` — matches UX spec §Design System Foundation.
- **Performance budgets**: Architecture defines ≤50ms introspection p95 and ≤500ms token issuance p95. UX spec's "Effective Permissions panel loads immediately" and "real-time preview (debounced 300ms)" are feasible within these budgets.
- **URL-as-truth tenant context**: Architecture confirms React Router v7 with `/tenants/:tenantId/...` URL structure — matches UX spec's Vercel-inspired tenant switching with sub-path preservation.
- **Command Palette**: Architecture includes shadcn/ui `Command` component — directly implements the ⌘K pattern.
- **Introspection enrichment**: `ITokenClaimsEnricher` pipeline produces `permissions`, `dimensional_attributes`, `license` — the data shape required by the Effective Permissions panel provenance chain.

---

### Alignment Issues Found

#### ⚠️ WARNING: "Plain-Language Dimensional Scope Summary" — Not Implemented in Epics

The UX spec identifies the **plain-language dimensional scope summary** as a **novel, OneId-specific UX pattern** — one of the four key design challenges and the third "Critical Success Moment." It is stated prominently:

> *"The UI must show the compiled meaning of assignments in plain language ('This user will see data from all branches in Bavaria belonging to ACME Group'), not just raw axis values."*

And in the core UX flow:

> *"Dimensional Attribute summary below with plain-language compiled meaning."*

**What exists in epics:** Story 5c.1 (Tenant Admin Management Pages) shows dimension assignments as axis/value pickers. Story 4a.5–4a.6 define the `DimensionValue` table and assignment API. Story 4b.3 defines the introspection response format (`dimensional_attributes: { axis: values[] }`).

**What is missing:** No UX-DR was written for the plain-language compiled meaning of dimension assignments, and no story implements it. The stories show raw axis/value assignments but not the synthesized summary (e.g., "This user can see data from ACME Group in Athens and Thessaloniki, BMW make, Sales and Service branches").

**Impact:** If not implemented, Tenant Admins see raw dimension values without understanding their combined effect — the primary UX value proposition for dimension assignment is lost.

**Recommendation:** Add a `UX-DR23: DimensionalScopeSummary` component (or add to an existing story in Epic 5b or 5c) that computes and displays the plain-language synthesis of a User's full dimension assignment set. At minimum this is a frontend display concern — the data is already in the introspection response.

---

#### ⚠️ WARNING: Token Storage on Page Refresh — UX Assumption Gap

- Architecture decision: **"tokens in JS memory"** — access tokens are never written to localStorage or cookies. Refresh token rotation enabled.
- UX spec assumes: *"Admin can close the tab with confidence"* (post-save experience); Internal Admin workflow is a continuous session across many tenants.
- **Gap:** If the refresh token is also stored only in JS memory (not an httpOnly cookie), every page refresh or tab close forces full re-authentication. This directly contradicts the UX goal of continuous, efficient Internal Admin sessions.
- Architecture says "Authorization Code Flow + PKCE directly from React" — standard SPA best practice is to store the refresh token in an httpOnly cookie (not in JS memory) to survive page refreshes while avoiding XSS exposure.
- **There is no story defining the refresh token storage mechanism** or the UX/browser behavior on page refresh.
- **Recommendation:** Clarify in the architecture decision whether the refresh token is stored in an httpOnly cookie (recommended) or in JS memory only. Add an acceptance criterion to Story 2.1 or an AR-15 covering refresh token persistence strategy. This has direct UX impact on session continuity.

---

#### ⚠️ MINOR: API Route Versioning Inconsistency

- Architecture document (§Naming Patterns): `GET /api/v1/tenants`, `POST /api/v1/tenants` — uses `/api/v1/` prefix.
- Epics (all stories): use `/api/internal/...` and `/api/tenant/...` — **no `/v1/` segment.**
- These are mutually inconsistent. If `/api/v1/` is the intended convention, all story acceptance criteria would need updating. If `/api/internal/` and `/api/tenant/` are the intended structure (which better reflects the two-tier admin split), the architecture naming pattern example should be updated.
- **Recommendation:** Decide and enforce one URL scheme. The `/api/internal/` + `/api/tenant/` structure in the epics is more semantically clear for the two-tier model and should be considered the canonical pattern — update the architecture example accordingly.

---

### Warnings

1. **No UX-DR covers the plain-language dimensional summary** — highest-value novel UX pattern in the specification has no implementation story.
2. **Session continuity on page refresh is undefined** — token storage strategy gap between architecture and expected UX behavior.
3. **API versioning discrepancy between architecture and epics** — minor but will cause confusion at the first story refinement session.

---

## Epic Quality Review

**Standards applied:** User value focus, epic independence, story independence, no forward dependencies, AC testability, database entity creation timing, greenfield readiness.

---

### Epic-Level Quality Assessment

#### 🟡 MINOR — Epic 1 and Epic 5a are Technical Milestones

**Epic 1 ("Foundation & Dev Infrastructure")** and **Epic 5a ("Frontend Shell & Core Components")** are technical/infrastructure epics. No end-user (Internal Admin, Tenant Admin, Tenant User) can perform any meaningful task after either epic completes in isolation.

- **Epic 1:** Delivers a compilable project, CI pipeline, Docker Compose stack, and test infrastructure. Zero user-facing value.
- **Epic 5a:** Delivers a React shell with dark-mode sidebar, empty pages, and skeleton loading states. No functional management surfaces.

**Verdict:** Both violate the "epics deliver user value" principle but are **accepted exceptions for greenfield projects.** The epics explicitly acknowledge this — Epic 1's goal text says "no end-user feature value, but the prerequisite for everything else" — which is honest. The design chain (Epic 1 → Epic 2 → ... → Epic 5c) progressively builds toward user value, and Epic 2 does deliver clear user value (authentication works end-to-end).

**Recommendation:** Accept as-is. The documentation of both epics is transparent about their technical nature. No change required. However, teams using velocity tracking should note that Epic 1 and 5a burn sprint capacity without user-facing deliverables — set stakeholder expectations accordingly.

---

#### 🟡 MINOR — Epic 4b Partial User Value at Story Level

**Epic 4b ("Token Evaluation & Overrides")** delivers the full permission evaluation pipeline. The epic goal describes the technical output (token evaluation, 500ms budget). While this is architecturally justified, it sits entirely below the user's visible surface — a Tenant User receives correct tokens; they don't interact with the pipeline.

**However:** Epic 4b contains Story 4b.1 (User-Level Permission Override Data Model) that delivers a new management API (Tenant Admin can create/view/delete overrides) — this IS user value. Story 4b.2 and 4b.3 deliver the evaluation logic. The split is logical within the epic.

**Verdict:** Accept as-is. Epic 4b is borderline but the split into 4a/4b is logical: 4a = manage the data, 4b = compute from the data.

---

### Story-Level Quality Assessment

#### 🔴 CRITICAL — Story 5c.2 References Non-Existent Backend Endpoint

**Story 5c.2 (F-1 New User Stepper)** AC:
> "submitting `POST /api/tenant/users` creates the user and navigates to the new User detail page"

There is **no backend story that defines this endpoint.** Story 5c.2 is a frontend story (Epic 5c is a frontend epic) and it tests calling a backend that does not yet have an implementation story.

**Result:** Story 5c.2's acceptance criterion cannot be independently verified — the endpoint is undefined. This is a forward dependency on missing work.

**This is the same gap identified in the Epic Coverage section (missing User Lifecycle Backend API).** The quality review confirms it breaks Story 5c.2's independence.

**Recommendation:** Add a new backend story (suggested: Story 3.8 or a new Epic 4a story) for `POST/GET/PUT/DELETE /api/tenant/users` before Story 5c.2 can be considered independently completable.

---

#### 🟠 MAJOR — The "Deferred Skip" Pattern Creates Stories with Incomplete ACs

The epics use a deliberate pattern of creating deliberately-skipped tests in early stories and completing them in later stories:

| Created in | Deferred to | Test |
|---|---|---|
| Story 1.1 | Story 2.1 | `DevSigningKeyStabilityTest` |
| Story 1.5 | Story 3.5 | `TestTokenFactoryContractTests` |
| Story 1.7 | Story 4a.1 | `PermissionCatalogSyncTests` |

Each early story explicitly creates the test with `[Fact(Skip = "Wired in Epic X")]` and the later story is required to remove the Skip and make it pass.

**The quality issue:** Each "creating" story is technically incomplete at delivery time — the test it creates cannot pass. CI shows green (the test is skipped, not failing) but the story's implicit quality gate is not closed.

**Risk:** If the "closing" story is delayed, deprioritized, or re-scoped, the Skipped test persists silently. Over time, multiple Skipped tests accumulate and the contract they enforce weakens.

**Verdict:** This is an accepted engineering tradeoff (make the gap visible vs. omit the test entirely) but it represents technical debt created intentionally. Stories 2.1, 3.5, and 4a.1 correctly include "Remove the Skip" as an explicit AC — the deferred obligation is tracked. Accept with caution.

**Recommendation:** Add a governance rule: no more than 3 deferred Skips may be open at any point in time. The three currently defined are at the limit. Do not introduce new deferred Skips without closing an existing one.

---

#### 🟠 MAJOR — Story 2.5 AC Contains an Explicit Non-Completion Instruction

**Story 2.5 (Token Introspection and jti Revocation Store)** AC:
> "Given TestTokenFactoryContractTests.cs still contains a [Fact(Skip)] — the Skip is NOT removed here — it remains deferred to Epic 3's licensing middleware story as per the established decision."

This AC instructs the implementer to deliberately leave a test in a skipped/incomplete state. This is unusual in a story's ACs — a story's ACs should define what DONE looks like, not what remains undone.

**Verdict:** The instruction is clear and the rationale is valid — the contract test requires the full licensing middleware which is Epic 3 work. However, framing it as an AC ("not removed here") is an antipattern. It should instead be a story-level note/assumption.

**Recommendation:** Move this instruction from the AC list to a `**Story Notes:**` section in Story 2.5. ACs should describe positive completion criteria only.

---

#### 🟡 MINOR — Epic 5b Has a Soft External Dependency Not in the Dependency Chain

Epic 5b states:
> "**Dependencies:** Epic 4b's introspection payload shape must be stable before this epic starts."

This is a legitimate dependency but it's specified as a prose note in the epic description, not in the formal dependency chain diagram shown at the end of the epics document:
```
Epic 1 → Epic 2 → Epic 3 → Epic 4a → Epic 4b → Epic 5b → Epic 5c
```

The diagram correctly shows this sequence. The prose note is redundant but harmless. ✓ No action required.

---

#### 🟡 MINOR — Story 1.7 Bundles Three Unrelated Concerns

**Story 1.7 ("ArchUnit Boundary Enforcement, Cache Abstraction, and DevSeeder")** combines:
1. ArchUnit boundary tests for the Internal Admin namespace
2. `ICacheService` abstraction wrapping `IMemoryCache`
3. DevSeeder with seed data

These are three independently deliverable pieces of work. The title acknowledges this ("and" + "and"). As a single story, this creates a situation where a developer working on DevSeeder must also deliver the cache abstraction and ArchUnit tests — or the story blocks on partial completion.

**Verdict:** MINOR quality issue. The story is deliverable but awkwardly bundled. For a team doing code review, the PR mixes three concerns. For a solo developer, it's manageable.

**Recommendation:** Consider splitting Story 1.7 into 1.7a (ArchUnit + Cache Abstraction — architectural constraints), 1.7b (DevSeeder — operational setup). Not critical for POC — acceptable as bundled for speed.

---

### Acceptance Criteria Quality

#### Overall Quality: Good

The epics use BDD (Given/When/Then) format consistently across all stories. ACs are specific, testable, and reference concrete test class names and assertion expectations. This is strong engineering practice — above average quality.

**Specific ACs reviewed and validated:**
- Story 2.2 lockout test: explicitly names `LockoutTriggeredIntegrationTest`, asserts state via identity store not just HTTP response ✓
- Story 3.5 seat enforcement: covers under-limit, at-limit, retroactive enforcement, and no-license-no-limit cases ✓
- Story 4b.2 DENY terminal test: names `DenyTerminalIntegrationTest`, covers expired DENY scenario ✓
- Story 4b.3 performance: `TokenEvaluationPerformanceTests.cs` with 50-sample minimum, 40ms ceiling (10ms headroom) ✓

**One AC that needs tightening:**

Story 2.4: "a single registration-order integration test confirms that all registered `ITokenClaimsEnricher` implementations are called in registration order" — "registration order" is measurable only if the test explicitly registers them in a known sequence and asserts call sequence. This should read: "a test registers two `ITokenClaimsEnricher` stubs with a known sequence (A before B) and asserts A's output is present in the identity before B runs." The current wording is ambiguous.

---

### Dependency Analysis Summary

**Within-Epic Dependencies (correct ordering confirmed):**
- Epic 1: Stories 1.1 → 1.2 → 1.3a → 1.3b → 1.4 → 1.5 → 1.6 → 1.7 (correct sequential build)
- Epic 2: Stories 2.1 → 2.2 → 2.3 → 2.4 → 2.5 → 2.6 → 2.7 (each builds on the previous — acceptable for this type of feature)
- Epic 3: Story 3.1 must be story 1 — correctly documented as gate for all other Epic 3 stories ✓
- Epic 4a: Story 4a.1 (`PermissionCatalog.cs`) must precede all other stories — correctly documented ✓
- Epic 4a: Story 4a.5 (`DimensionValue` table) must precede Story 4a.6 (dimension assignments) — correctly documented ✓
- Epic 4b: Story 4b.1 (schema) before Story 4b.2 (evaluation pipeline) before Story 4b.3 (enriched introspection + performance) ✓

**Cross-Epic Dependencies (correctly documented):**
- Story 2.5 → Story 3.5: TestTokenFactory contract test deferred (documented)
- Epic 4b depends on Epic 4a schema stability (documented)
- Epic 5b depends on Epic 4b introspection payload shape (documented)
- Epic 5c Story 5c.2 depends on non-existent User lifecycle backend — ❌ undocumented gap

**Database Entity Creation Timing:**
- All entities are created in the story where they are first needed ✓
- `UseXminAsConcurrencyToken()` is applied per-entity at the story level with explicit enumeration ✓
- No bulk "create all tables" story exists ✓

---

### Best Practices Compliance Checklist

| Epic | Delivers User Value | Independence | Stories Sized Well | No Forward Deps | DB Created When Needed | Clear ACs |
|---|---|---|---|---|---|---|
| Epic 1 | ⚠️ Developer only | ✓ | ⚠️ Story 1.7 bundled | ✓ (noted deferred skips) | ✓ | ✓ |
| Epic 2 | ✓ | ✓ | ✓ | ⚠️ Deferred Skips | ✓ | ✓ |
| Epic 3 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Epic 4a | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Epic 4b | ⚠️ Indirect | ✓ | ⚠️ 4b.1 alone lacks closure | ✓ | ✓ | ✓ |
| Epic 5a | ⚠️ Developer only | ✓ | ✓ | ✓ | ✓ | ✓ |
| Epic 5b | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Epic 5c | ✓ | ✓ | ❌ Story 5c.2 missing backend dep | ❌ Story 5c.2 | ✓ | ✓ |
| Epic 6★ | ✓ (stretch) | ✓ | ✓ | ✓ | ✓ | ✓ |

---

## Summary and Recommendations

### Overall Readiness Status

## ⚠️ NEEDS WORK

The planning artifacts for OneId are **of excellent overall quality** — the PRD is thorough and traceable, the architecture is well-reasoned, the UX specification is comprehensive, and the epics contain well-structured BDD acceptance criteria above industry average. However, **3 issues must be resolved before Epic 1 Sprint 1 begins.** Without addressing them, implementation will stall mid-way through Sprint 3 or 5.

---

### Critical Issues Requiring Immediate Action (Before Sprint 1)

#### 1. 🔴 Missing Backend Story: User Lifecycle API (Tenant Admin)

There is no story defining the `POST/GET/PUT/DELETE /api/tenant/users` endpoints. This is required by FR-21 and FR-18, and Story 5c.2's acceptance criteria reference a `POST /api/tenant/users` endpoint that will not exist at implementation time.

**Action:** Write and approve a new Story (suggested: Story 4a.7 "User Lifecycle Management — Tenant Admin") before Epic 4a stories are worked. It must cover: create user (with initial credentials), read user, update user profile, deactivate user (freeing a seat), and list users. Extend `TenantIsolationRegressionTests.cs` and apply `UseXminAsConcurrencyToken()`.

---

#### 2. 🔴 Missing Backend Story: Audit Log Infrastructure

Story 5c.5 (Audit Log UI) calls `GET /api/tenant/audit` and `GET /api/internal/audit`, but no backend story defines the `AuditLog` entity, the audit emission service, or these endpoints. The `AuditLogPerformanceTest` referenced in Story 5c.5 is a backend test with no home epic.

**Action:** Write and approve a new Story (suggested: add to Epic 3 as Story 3.8 "Audit Log Infrastructure") covering: `AuditLog` entity + EF Core migration, audit event emission service (wired into all management operations from Epic 3 onward), and the two paginated read endpoints. Stories 3.2–3.7 should emit audit events through this service.

---

#### 3. 🔴 Token Architecture Contradiction Must Be Formally Closed

The PRD addendum states "Decision: Reference tokens chosen" (opaque tokens, no JWT). The PRD body FR-5, the entire epics document, and every implementation story describe a **signed JWT (RS256) with server-side jti for revocation** — a hybrid JWT model, not reference tokens.

These are fundamentally different OpenIddict configurations. Building against the wrong model wastes the entire Epic 2 sprint.

**Action:** Update the PRD addendum. Explicitly state: "The reference token option was considered and rejected. The implemented design is: signed JWT (RS256) with `roles` in payload + server-side `jti` record for revocation + enriched introspection for permissions/dimensions/license." Remove or retract the "Reference tokens chosen" sentence. This takes 15 minutes and prevents a potentially catastrophic misunderstanding at sprint kickoff.

---

### Major Issues (Should Address Before Sprint 3)

| # | Issue | Impact | Effort |
|---|---|---|---|
| 4 | FR-16/17 (federation) marked stretch goal but listed In-Scope in PRD §8.1 | Stakeholder expectation mismatch at POC demo | Update PRD §8.1 or re-commit Epic 6 |
| 5 | PRD FR-11 says "purely additive" but epics implement DENY overrides | Developer reads PRD for authority and implements wrong model | Update PRD FR-11 text |
| 6 | Token storage on page refresh undefined (architecture says JS memory; UX assumes continuous sessions) | Internal Admins logged out on page refresh; major UX regression | Clarify AR-15: refresh token in httpOnly cookie vs memory |
| 7 | Story 5c.2 AC references `POST /api/tenant/users` that doesn't exist | Story 5c.2 cannot be independently verified — blocks Epic 5c Sprint | Resolved by Issue #1 above |

---

### Minor Issues (Address During Implementation as Encountered)

| # | Issue | Recommendation |
|---|---|---|
| 8 | Open Question 3 not closed in PRD (dimension source of truth) | Add "Closed: maintained in OneId via DimensionValue table" to PRD §10 |
| 9 | "RoleGroups" (PRD) vs "Role Sets" (Epics) terminology | Update PRD FR-8 to "Role Sets" |
| 10 | FR-15 appears in §4.5 but §4.4 skips from FR-14 to FR-16 | Renumber or add a note in PRD FR-15 header |
| 11 | Plain-language dimensional scope summary — no UX-DR or story | Add UX-DR23 and a story in Epic 5b for `DimensionalScopeSummary` component |
| 12 | API versioning: architecture says `/api/v1/`; epics use `/api/internal/` and `/api/tenant/` | Update architecture naming example to match epics — use `/api/internal/` and `/api/tenant/` |
| 13 | Story 2.4 "registration order" AC is ambiguous | Tighten to: "registers stubs A then B; asserts A enriches identity before B runs" |
| 14 | Story 2.5 AC contains a "do NOT complete this" instruction | Move to `**Story Notes:**` section, not inside ACs |
| 15 | Story 1.7 bundles 3 unrelated concerns | Split into 1.7a (ArchUnit + Cache) and 1.7b (DevSeeder) if team velocity warrants |
| 16 | Deferred Skip governance | Cap at 3 open deferred Skips; document as a team convention |

---

### Issue Inventory by Category

| Category | Critical | Major | Minor |
|---|---|---|---|
| Missing stories | 2 | 0 | 1 |
| Architecture contradictions | 1 | 1 | 1 |
| PRD accuracy | 0 | 2 | 3 |
| Story quality | 0 | 2 | 4 |
| UX alignment | 0 | 1 | 0 |
| **Total** | **3** | **6** | **9** |

---

### Recommended Sequence Before Sprint 1

1. **Today (30 minutes):** Update PRD addendum — close the token architecture decision. Add one sentence: "The JWT hybrid approach (signed JWT with jti revocation + enriched introspection) is the final design."
2. **Before Epic 3 sprint planning:** Write Story 3.8 (Audit Log Infrastructure) and add it to the Epic 3 backlog.
3. **Before Epic 4a sprint planning:** Write Story 4a.7 (User Lifecycle API) and add it to the Epic 4a backlog.
4. **Before Epic 5c sprint planning:** Update Story 5c.2 to reference the backend endpoint from Story 4a.7 as a prerequisite.
5. **Alignment session (1 hour):** Resolve Issue #4 (federation scope) and Issue #6 (token refresh storage) with the team.

---

### Final Note

This assessment identified **18 issues** across 6 categories. **3 are critical** — they will cause implementation to stall or diverge from design if not addressed before Sprint 1. The remaining 15 are quality improvements that raise clarity and reduce mid-sprint surprises.

The planning artifacts are **substantially ready.** The architecture is sound, the epics are well-structured and thoroughly acceptance-criterion'd, and the UX specification is comprehensive. The critical issues are fixable in hours, not days. With those three resolved, the OneId implementation has a clean, implementable baseline.

---

*Report generated: 2026-05-21*
*Assessed by: Claude (bmad-check-implementation-readiness)*
*Documents assessed: PRD (prd.md + addendum.md), Architecture (architecture.md), UX (ux-design-specification.md), Epics (epics.md)*
