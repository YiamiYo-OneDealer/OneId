# PRD Addendum — OneId

Technical depth and options-considered notes that support the PRD but belong downstream in the architecture document.

---

## OpenIddict Token Pipeline — Technical Notes

Custom claims in OpenIddict are injected via `IOpenIddictServerHandler<ProcessSignInContext>`. Key implementation constraints:

- Every custom claim must have its destination set explicitly: `claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken)`. Claims without a declared destination are **silently dropped** from the token — this will not throw an exception and is the single most common implementation mistake.
- Handler ordering matters. Handlers registered with `AddBefore`/`AddAfter` can overwrite each other's claims or cause validation failures if ordered incorrectly. The Permission + Dimensional Attribute population handler must run after the base principal is established.
- Claims from external (upstream IDP) authentication are **not** automatically available in OpenIddict's pipeline. They must be manually read from `AuthenticateResult` during the external sign-in callback and stored in the OneId User record or session before OpenIddict's token pipeline runs.

**Version note:** API stabilized in OpenIddict v3/v4 and is consistent through v5 (released 2024). v5 introduced breaking changes to the server options API; pre-v4 community samples should not be used without review. Cross-check with official documentation at documentation.openiddict.com.

---

## Token Architecture — Decision Record

**Decision: JWT hybrid model chosen. Reference tokens rejected.**

RS256-signed JWTs with server-side `jti` revocation records are the canonical token format. The full enriched claim set (roles, permissions, dimensional attributes, license state) is embedded in the token payload and returned via the enriched introspection endpoint. The two options considered were:

**Option A — JWT hybrid model (chosen)**
- OneDealer v2 validates the RS256 signature locally on every request — no round-trip to OneId for routine validation.
- Selective revocation via server-side `jti` table: OneId tracks issued token IDs; introspection checks the revocation table and returns enriched claims on demand.
- Token payload is bounded at issuance; stale claims are mitigated by short access token lifetime (15 min) and the `jti` revocation mechanism.
- Size risk: large Permission arrays (50+ strings of ~20–30 chars each) + Dimensional Attribute values easily reach 3–5 KB before OIDC envelope. Realistic worst case (broad admin, all 5 dimension axes with multiple values) should be measured during POC.
- Safe ceiling: 4–6 KB per token payload (Nginx header buffer default 8 KB; AWS ALB total header cap 16 KB).
- OpenIddict config: `UseReferenceTokens(false)` (default); RS256 signing key configured via `AddSigningCertificate` or `AddDevelopmentSigningCertificate`.

**Option B — Reference tokens + introspection (rejected)**
- OpenIddict stores the token payload in the database; issues an opaque reference instead of a JWT.
- OneDealer v2 calls OneId's introspection endpoint (`/connect/introspect`) on each request to validate and retrieve claims.
- Eliminates token size concern entirely.
- **Rejected because:** adds a network round-trip per request; introduces OneId as a hard runtime availability dependency of OneDealer v2; EF Core token store becomes a high-read bottleneck under load. These trade-offs are unacceptable for a multi-tenant production system.

**Key implications of the chosen model:**
- All epics describing `ITokenClaimsEnricher`, the `jti` revocation table, and enriched introspection endpoint are aligned with this decision.
- The introspection endpoint (`/connect/introspect`) is still used by resource servers that cannot validate JWTs locally (e.g., legacy OneDealer modules), but it is supplementary, not the primary validation path.
- Token size must be validated during POC using a realistic worst-case claim set before Epic 4b stories are closed.

---

## SAML2 Library Options

OpenIddict has no native SAML2 support. Two ASP.NET Core-compatible libraries:

**Sustainsys.Saml2**
- Open source (LGPL), actively maintained, large community.
- ASP.NET Core middleware integration; plugs into external auth pipeline.
- Supports SP-initiated and IdP-initiated flows.
- More widely used in .NET ecosystem; more Stack Overflow coverage.

**ITfoxtec.Identity.Saml2**
- Open source (BSD), lighter weight.
- Less middleware-integrated; requires more manual wiring.
- May be simpler for basic SP-initiated flows.

**Recommendation:** Sustainsys.Saml2 for its maintained status and community. Validate ASP.NET Core version compatibility before committing.

---

## Licensing State in JWT — Design Rationale

The decision to embed `license.status` (active/exceeded) rather than live seat counts in the token is deliberate:

- Seat counts are mutable — they change as Users authenticate or are deactivated. Embedding a count produces a stale value immediately.
- `status` is checked at token issuance (FR-18) when OneId has the authoritative current count. The status at issuance is accurate; it may become stale during the token's lifetime if a Tenant crosses its limit while the session is active.
- Acceptable for POC: a User already authenticated remains active until token expiry even if the Tenant subsequently exceeds its limit. Re-authentication will be denied.
- For future concurrent and usage-based models: status encoding will need to be extended, but the claim structure (`model`, `status`) is already extensible.

---

## RoleGroup — Authorization Model Clarification

RoleGroups are a management convenience entity, not an authorization entity. They do not appear in issued tokens. Their function:

- Group Roles into named sets (e.g., "Sales Bundle" = Invoice Role + Pricing Role + Customer Role).
- Assign a RoleGroup to a Group instead of assigning Roles one at a time.
- At token issuance, the Permission evaluation traverses: User → Groups → (Roles + RoleGroups → Roles) → Permissions.
- The emitted `permissions` claim is always the flat resolved Permission string array; no intermediate structure is preserved.
