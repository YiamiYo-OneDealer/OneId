---
title: "Product Brief: OneId"
status: final
created: 2026-05-21
updated: 2026-05-21
---

# Product Brief: OneId

## Executive Summary

OneId is a custom identity and licensing platform being built as the authentication and authorization backbone for OneDealer v2 — a full ground-up rebuild of OneDealer's CRM/ERP product for automotive dealerships.

OneDealer's authorization model is unusually complex for an off-the-shelf IDP: it combines multi-tenant user management with a hybrid RBAC+ABAC model — fine-grained permissions controlling what users can do, and dimensional attributes (company, location, branch, make, market segment) controlling what data they can see across every entity in the system. No standard IDP maps onto this without fighting its own data model.

OneId addresses this by building on OpenIddict — a .NET-native OAuth2/OIDC server framework — with custom claim types, a purpose-built management UI, IDP chaining for upstream federation, and an integrated licensing authority that embeds license state directly into issued tokens. It serves three user types: internal OneDealer admins with full platform access, tenant administrators who manage their own users within their tenant scope, and dealership staff who authenticate and operate within their assigned context.

## The Problem

OneDealer has historically managed user credentials and access control in an ad-hoc, application-coupled way. As the product matured, this created increasing maintenance burden and risk: no protocol standards compliance, no clean federation story, and an authorization model embedded too close to application logic. Adding SAML2 and Okta support as bolt-ons confirmed the need for a proper identity layer.

Specific gaps in evaluated off-the-shelf solutions:

- Standard RBAC does not accommodate fine-grained permissions combined with multi-dimensional ABAC scoping
- Multi-tenancy models (realms, organizations) in standard IDPs don't reflect OneDealer's tenant and dimensional structure
- Dimensional attributes drive row-level data filtering across the entire application — this is not a feature-gate problem, it is a data access problem that must be reflected accurately in every issued token
- Licensing has no natural home in standard IDPs; it would require a separate runtime service
- Management UI complexity requires deep customization regardless of tool chosen

## The Solution

OneId is built on OpenIddict with custom extensions for:

- **Hybrid RBAC + ABAC authorization:** Users are assigned to Groups; Groups hold Roles; Roles bundle Permissions (fine-grained, string-identified operations such as `crm.invoice.create` or `erp.pricing.discount.approve`). Separately, users are assigned dimensional attributes (Company, Location, Branch, Make, MarketSegment) that scope what data they can access across the entire system.
- **Row-level security via JWT claims:** Dimensional attributes are embedded in issued tokens as claims. OneDealer consumes these claims to filter all data queries — the IDP is the authoritative source of a user's dimensional context.
- **Multi-tenancy:** Tenant-scoped identity and permission management, with scoped administrator views per tenant.
- **IDP chaining:** Federation with upstream IDPs (SAML2, Okta) via OpenIddict's chaining support, preserving existing client integrations.
- **Licensing authority:** Seat-count licensing managed within OneId and emitted as JWT claims; data model designed for extension to concurrent and usage-based models.
- **Management UI:** Purpose-built React admin console for internal admins and tenant-level administrators covering users, groups, roles, permissions, dimensional assignments, and licensing.
- **MFA:** Simple multi-factor authentication included at POC stage.

## POC Risks and Open Questions

- **Token size:** The combined claim set (permissions, dimensional attributes, license state) may approach JWT size limits for users with broad access. Needs validation under realistic data.
- **OpenIddict extensibility:** Custom claim types and licensing logic must integrate cleanly with OpenIddict's pipeline without breaking standard OIDC compliance. Extent of required customization is unconfirmed.
- **Teams:** Deferred from POC. Whether Teams function as an ABAC dimension or an organizational grouping construct affects the final data model.
- **Permission migration:** Existing numeric IDs map one-to-one to string identifiers in production. Migration path and tooling are out of POC scope.

## Success Criteria

The POC succeeds if it demonstrates:

1. OpenIddict can be extended to issue tokens carrying the full OneDealer claim set — permissions, dimensional attributes, and license state — without breaking standard OIDC compliance, and that the combined claim set remains within practical JWT size limits
2. The full authorization model (Users → Groups → Roles → Permissions, scoped by dimensional attributes) can be managed end-to-end via the management UI
3. IDP chaining with SAML2 and Okta works without disrupting existing integrations
4. Seat-count licensing is correctly enforced at token issuance and verifiable from JWT claims alone
5. Tenant administrators can manage their own users within their tenant scope without accessing other tenants' data

## Scope

**In scope (POC):**

- Core OAuth2/OIDC flows: authorization code, client credentials, token refresh
- Full authorization model: Users, Groups, Roles, Permissions (string-identified, e.g., `crm.invoice.create`), dimensional attributes (Company/Location/Branch/Make/MarketSegment), Tenants
- Simple MFA
- Management UI (React): user, group, role, permission, dimensional assignment, licensing, and tenant administration — with scoped tenant-admin views
- IDP chaining: SAML2 and Okta federation
- Seat-count licensing with data model designed for concurrent and usage-based extension
- Authentication UI: login, MFA, password reset

**Out of scope (POC):**

- End customer (car buyer) authentication
- Integration with systems other than OneDealer v2
- Concurrent and usage-based licensing implementation (data model designed, not implemented)
- Teams (model TBD)
- Production hardening, performance testing, SLA design

## Vision

If the POC validates feasibility, OneId becomes the canonical identity and licensing platform for the OneDealer product suite. The licensing authority, once embedded, becomes a strategic lever: feature gating, consumption-based billing, and tenant-level entitlement management all flow through a single, standards-compliant platform the team fully owns.
