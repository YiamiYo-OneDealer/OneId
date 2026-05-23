# Sprint Change Proposal — UI-First Resequencing
**Date:** 2026-05-23
**Scope:** Minor — reordering only, no scope changes
**Approved by:** Georgios Mathioudaki

---

## Section 1: Issue Summary

**Problem statement:** The original sprint plan sequences all backend work (Epics 2–4b) before the frontend is built. Stakeholders want to see a working UI with basic user management — users, groups, roles, role sets, and permissions — before licensing and the full permission evaluation pipeline are complete.

**Context:** Epic 1 is fully done. All stories remain in the plan unchanged. This is a pure resequencing to front-load the frontend shell and user management CRUD pages ahead of auth and backend work.

**Discovery:** Stakeholder demo milestone requirement raised during sprint planning.

---

## Section 2: Impact Analysis

**Epic Impact:**

| Epic | Change |
|---|---|
| Epic 5a | **Moved to Phase 1** — starts first, before Epic 2 (no backend dependency) |
| Epic 2 | **Moved to Phase 2** — after Epic 5a is complete |
| Epic 3 | **Split** — non-licensing stories (3.1, 3.2, 3.4, 3.6, 3.8) in Phase 3; licensing stories (3.3, 3.5, 3.7) deferred to Phase 6 |
| Epic 4a | Phase 4 (unchanged relative order) |
| Epic 5b | **Split** — hooks/pure-component stories (5b.1, 5b.2, 5b.6) in Phase 4; introspection-dependent stories (5b.3, 5b.4, 5b.5) remain in Phase 8 after Epic 4b |
| Epic 5c | **Split** — CRUD pages (5c.1, 5c.3, 5c.4, 5c.5, 5c.6) are the Phase 5 UI Demo Milestone; introspection-dependent 5c.2 and accessibility audit 5c.7 remain in Phase 8 |
| Epic 4b | Phase 7 (unchanged relative order — still after 4a) |
| Epic 6 | Phase 9 — unchanged (stretch goal) |

**Artifact Conflicts:** None. No PRD, architecture, or UX specification changes required.

**Technical Impact:** None. All implementation details, acceptance criteria, and architectural constraints are unchanged.

---

## Section 3: Recommended Approach — Direct Adjustment

**Hard dependency constraints preserved:**

- Epic 5b.3/5b.4/5b.5 remain after Epic 4b (require enriched introspection payload) ✓
- Epic 5c.2 (real-time permissions preview in New User stepper) remains after Epic 4b ✓
- Epic 4a precedes Epic 5c.1 (CRUD APIs must exist before management pages) ✓
- Epic 3.1 (ITenantContext) precedes Epic 4a ✓
- Epic 2 precedes Epic 3 (auth infrastructure required for tenant context) ✓

**Implementation note for Phase 1 (Epic 5a):** Story 5a.2 (App Shell, routing, tenant context) wires the auth context and `useHasPermission` prefetching. During Phase 1, these components operate with a stub/mocked auth state. Real auth is connected in Phase 2 (Epic 2) without requiring any rework of the shell — the integration point is an environment variable or config flag.

---

## Section 4: New Phased Execution Plan

```
[Phase 1]  Epic 5a — Frontend Shell & Core Components
           ★ Pure frontend — no backend dependency ★
           Stories: 5a.1 → 5a.2 → 5a.3 → 5a.4 → 5a.5

[Phase 2]  Epic 2 — Working Authentication (backend)
           Stories: 2.1 → 2.2 → 2.3 → 2.4 → 2.5 → 2.6 → 2.7 → 2.8 → 2.9

[Phase 3]  Epic 3 — Non-licensing stories only
           Stories: 3.1 → 3.2 → 3.4 → 3.6 → 3.8
           (3.1 must be first — all others depend on ITenantContext)

[Phase 4]  Epic 4a — Authorization Data Model + Epic 5b partial (parallel)
           Epic 4a: 4a.1 → 4a.2 → 4a.3 → 4a.4 → 4a.5 → 4a.6 → 4a.7
           Epic 5b (parallel): 5b.1, 5b.2, 5b.6

[Phase 5]  ★ UI DEMO MILESTONE ★
           Epic 5c CRUD pages: 5c.1 → 5c.3 → 5c.4 → 5c.5 → 5c.6

           Demo delivers:
           - Working login with TOTP MFA
           - Design system shell + GlobalNav + AdminTierBanner
           - User management CRUD (create, edit, deactivate users)
           - Group management CRUD
           - Role management CRUD
           - Role Set management CRUD
           - Dimension assignment UI (reference lists + per-user assignments)
           - Internal Admin pages: Tenants, Permissions catalog, IDP placeholder,
             Tenant Admin designation
           - Tenant provisioning stepper (F-3)
           - Audit Log UI
           - CommandPalette (⌘K)

[Phase 6]  Epic 3 licensing: 3.3 → 3.5 → 3.7
           (seat-count license CRUD, seat limit enforcement, license view)

[Phase 7]  Epic 4b — Token Evaluation & Overrides
           Stories: 4b.1 → 4b.2 → 4b.3

[Phase 8]  Epic 5b permission/override UX + Epic 5c remaining
           Epic 5b: 5b.3 → 5b.4 → 5b.5
           Epic 5c: 5c.2 → 5c.7

[Phase 9]  Epic 6 ★ Federated Authentication (stretch goal — unchanged)
           Stories: 6.1 → 6.2 → 6.3 → 6.4
```

---

## Section 5: Implementation Handoff

**Scope classification:** Minor — Developer agent implements directly.

**Files updated by this proposal:**
- `_bmad-output/planning-artifacts/epics.md` — dependency chain updated
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — phase labels added

**Next step:** Pick up Epic 5a Story 5a.1 (Design System Foundation) as the first story.
Use `bmad-create-story` for Story 5a.1 or `bmad-dev-story` if the story file already exists.

**Success criteria for Phase 5 (UI Demo Milestone):**
- A Tenant Admin can log in and perform full CRUD on users, groups, roles, and role sets
- An Internal Admin can provision tenants and manage the permission catalog
- The audit log is readable
- The ⌘K CommandPalette is functional
- No EffectivePermissionsPanel, no DENY overrides, no licensing UI required for the demo
