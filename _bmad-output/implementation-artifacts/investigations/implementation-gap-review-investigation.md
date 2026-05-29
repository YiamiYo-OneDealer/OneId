# Investigation: Implementation Gap Review

## Hand-off Brief

1. **What happened.** Four backend endpoints critical to the EffectivePermissionsPanel UX chain were never built (`GET /effective-permissions`, `POST /effective-permissions/preview`, `POST /revoke-tokens`, plus the licensing API trifecta); the frontend code explicitly documents the mock-data fallback and these four stories remain in "review" status rather than "done".
2. **Where the case stands.** Three categories of gap identified: (A) missing backend endpoints blocking Phase 8 stories, (B) Phase 6 licensing stories intentionally deferred but not started, (C) a set of stories marked "review" whose frontend exists but backend is absent.
3. **What's needed next.** Decide priority order: unblock Phase 8 (build the three missing tenant-scoped endpoints) or start Phase 6 licensing (3.3 → 3.5 → 3.7) first.

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | N/A                                                                        |
| Date opened      | 2026-05-29                                                                 |
| Status           | Active                                                                     |
| System           | OneId — .NET 8 backend + React/Vite frontend                               |
| Evidence sources | Story artifact files, codebase file listing, controller source code, frontend api.ts comment |

## Problem Statement

User suspects core functionality was marked done but not actually built. Cross-referencing story status fields against actual code.

## Evidence Inventory

| Source   | Status    | Notes |
| -------- | --------- | ----- |
| Story artifact `.md` files (55 files) | Available | Status fields read directly |
| Backend `Controllers/*.cs` (12 files) | Available | Full list confirmed via glob |
| Frontend `features/users/api.ts` | Available | Explicit mock-data comment found |
| `deferred-work.md` | Available | License stub documented as known gap |
| Git log | Available | Latest: "Fix permissions", prior "Story 5c-3b done" |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| Phase 1–5 | UI Demo Milestone executed per sprint change proposal | Story files + code | Confirmed |
| Phase 7 (4b) | Epic 4b done: permission evaluation pipeline + enriched introspection backend | Story files: 4b.1/4b.2/4b.3 Status=done | Confirmed |
| Phase 8 start | 5b.3/5b.4/5b.5/5b.6 opened, frontend components built | Story files: Status=review | Confirmed |
| Phase 8 stall | Backend endpoints for effective-permissions never created; frontend stayed on mock data | `api.ts` comment + controller glob | Confirmed |
| 2026-05-28 | Code review of 4b, 4a-7, 5c-3b, 5c-2, 5c-7 | `deferred-work.md` timestamps | Confirmed |
| Latest | Story 5c-3b marked done (permission management UI) | Git log | Confirmed |

---

## Confirmed Findings

### Finding 1: No backend endpoint for `GET /api/tenant/users/{userId}/effective-permissions`

**Evidence:** `src/OneId.Server/Controllers/TenantUsersController.cs` — controller has GET (list), GET (by id), POST, PATCH, DELETE only. Grep across all `Controllers/*.cs` for `effective.perm` returns zero matches.

**Detail:** Story 5b.3 requires `useEffectivePermissionsLive` to call `GET /api/tenant/users/{userId}/effective-permissions`. This endpoint does not exist in any controller.

---

### Finding 2: No backend endpoint for `POST /api/tenant/effective-permissions/preview`

**Evidence:** Same controller grep. `TenantUsersController` has no preview action. No other controller matches.

**Detail:** Story 5b.4 requires a debounced POST to this endpoint. Missing.

---

### Finding 3: No backend endpoint for `POST /api/tenant/users/{userId}/revoke-tokens`

**Evidence:** Grep for `revoke.token|revoke-token|tokens/revoke` across all `src/OneId.Server` `.cs` files returns zero matches. Frontend calls `api/tenant/users/${userId}/revoke-tokens` in `useRevokeUserTokens`.

**Detail:** Story 5b.5 `DenyOverrideSheet` "Force Re-authenticate" button requires this endpoint. Endpoint does not exist.

---

### Finding 4: Frontend EffectivePermissionsPanel is on mock data

**Evidence:** `src/OneId.Web/src/features/users/api.ts:9–12` — explicit comment:
```
// Effective permissions require a confidential-client introspection call that the SPA cannot
// make directly. These hooks remain on mock data until a dedicated /api/account/permissions
// endpoint is added.
```
Both `effectivePermissionsLiveOptions` (queryFn calls `mockStore.getEffectivePermissions`) and `useEffectivePermissionsPreview` (calls `mockStore.getEffectivePermissionsPreview`) are on mock data.

**Detail:** The whole EffectivePermissionsPanel chain — Capabilities tab, Permission Details tab, ProvenanceChain, DenyOverrideBadge inline display — renders mock data, not live backend data. This affects Story 5b.3, 5b.4, and transitively 5c.2 (New User stepper's real-time preview also uses mock).

---

### Finding 5: Stories 5b.3, 5b.4, 5b.5, 5b.6 are in "review" (not done)

**Evidence:** Story file Status fields: all four read `Status: review`.

**Detail:** Phase 8 incomplete. The frontend components exist (components confirmed by codebase survey: `EffectivePermissions.tsx`, `DenyOverrideSheet.tsx`, `SeatUsageIndicator.tsx`, `DimensionalScopeSummary.tsx` all present) but backend side is missing, which is why stories are stuck in review.

---

### Finding 6: Epic 3 licensing stories (3.3, 3.5, 3.7) not started

**Evidence:** No story artifact files exist for 3.3, 3.5, or 3.7. `deferred-work.md` documents: "F11: License stub always returns `status: active` — by design; Phase 6 stories 3-3/3-5 will wire real seat-count and license status data."

**Detail:** Phase 6 (seat-count license CRUD, enforcement at token issuance, license view endpoint) was never started. This is a planned deferral per the sprint change proposal, but it means `SeatUsageIndicator` always shows a stub value, and no tenant can be capped.

---

### Finding 7: Stories 5a.2–5a.5 and Epic 2 (most) are in "review"

**Evidence:** Status fields: 5a.2/5a.3/5a.4/5a.5 = review; Epic 2 stories 2.1–2.8 = review.

**Detail:** This is likely a bookkeeping gap, not an implementation gap. The codebase confirms GlobalNav, AppShell, DataTable, OpenIddict auth, TOTP, JWT issuance all exist. These stories were probably implemented but never formally marked done after review. The one exception: Story 2.8 (SPA token lifecycle / refresh token in-memory storage) — frontend may still use localStorage (see `deferred-work.md`: "D1 (5c-7): Auth tokens persisted to localStorage plaintext — Intentional fix for Playwright E2E tests"). This deserves a separate check.

---

## Deduced Conclusions

### Deduction 1: Phase 8 is incomplete because backend endpoints for effective-permissions were never built

**Based on:** Findings 1, 2, 3, 4, 5

**Reasoning:** Epic 4b built the permission evaluation pipeline on the server side (enriched introspection). But introspection uses confidential client credentials — the SPA cannot call it directly. A separate tenant-facing endpoint must proxy or re-evaluate permissions. This endpoint was not built as part of 4b.3 and was not tracked as a story gap. The frontend authors noted this explicitly in code comments and put mock data in place. Stories 5b.3/5b.4/5b.5 went into "review" blocked on this backend gap.

**Conclusion:** The entire EffectivePermissionsPanel live + preview loop, the New User stepper's real-time preview (5c.2), and Force Re-authenticate (5b.5) are not end-to-end functional against the real backend.

---

### Deduction 2: Story 5c.2 "done" is a partial truth

**Based on:** Findings 4, 5

**Reasoning:** 5c.2 is marked done and the stepper UI exists, but its EffectivePermissionsPanel preview mode calls `mockStore.getEffectivePermissionsPreview` — not a real backend endpoint. The story's primary differentiator (real-time permissions before save) is running on fabricated data.

**Conclusion:** 5c.2 frontend UX is complete but the end-to-end story acceptance criterion ("shows the projected permission set") is not met against the real backend.

---

## Hypothesized Paths

### Hypothesis 1: `GET /api/tenant/users/{userId}/effective-permissions` was expected to emerge from 4b.3 but was missed in scope

**Status:** Open

**Theory:** Story 4b.3 focused on enriching the introspection response. The team may have assumed the frontend could call introspection, not realising the SPA cannot (no client secret). The gap became visible when 5b.3 started and the `api.ts` comment was added as a placeholder.

**Would confirm:** Check 4b.3 story file acceptance criteria for any mention of this endpoint.

**Would refute:** Finding a merged controller or handler for effective-permissions that the glob missed.

---

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------ | ------------- |
| Does 4b.3 AC mention the tenant-facing effective-permissions endpoint? | Would confirm Hypothesis 1 | Read `4b-3-enriched-introspection-response-and-performance-gate.md` AC section |
| Is `useHasPermission` connected to a real endpoint or also mock? | Determines if permission-gated UI works in production | Read `src/OneId.Web/src/hooks/useHasPermission.ts` |
| Story 2.8 — are tokens in localStorage or memory? | Security concern flagged in deferred-work | Read `src/OneId.Web/src/lib/auth.ts` |

---

## Source Code Trace

| Element | Detail |
| ------- | ------- |
| Mock data anchor | `src/OneId.Web/src/features/users/api.ts:12–20` — `effectivePermissionsLiveOptions` queryFn → `mockStore.getEffectivePermissions` |
| Missing backend | No file in `src/OneId.Server/Controllers/` matches `effective-perm` |
| Token revoke frontend call | `src/OneId.Web/src/features/users/api.ts:96–99` — `useRevokeUserTokens` → `api/tenant/users/${userId}/revoke-tokens` |
| Token revoke backend | Zero grep matches across `src/OneId.Server/**/*.cs` |
| License stub | `deferred-work.md` F11 — `IntrospectionResponseEnricher` returns `status: active` always |

---

## Conclusion

**Confidence:** High

Three backend endpoints are confirmed absent: `GET /effective-permissions`, `POST /effective-permissions/preview`, `POST /revoke-tokens`. The frontend is explicitly on mock data for the entire EffectivePermissionsPanel chain. Phase 6 licensing (3.3/3.5/3.7) is also not started but is a planned deferral. Stories 5b.3/5b.4/5b.5/5b.6 reflect this reality by remaining in "review" status. The majority of Epic 2 and 5a stories in "review" appear to be a bookkeeping issue (code exists, not formally marked done).

---

## Recommended Next Steps

### Fix direction

**Priority 1 — Unblock Phase 8 (missing backend endpoints):**
- Build `GET /api/tenant/users/{userId}/effective-permissions` — calls `PermissionEvaluator` (already exists from 4b.2) scoped to the user, returns permissions + provenance chain for the frontend.
- Build `POST /api/tenant/effective-permissions/preview` — takes a preview payload (simulated group assignments), runs through the evaluator, returns the projected permission set.
- Build `POST /api/tenant/users/{userId}/revoke-tokens` — calls the jti revocation service (already exists from 2.6) for a specific user.

**Priority 2 — Phase 6 licensing (planned):**
- Story 3.3: Seat-count license CRUD
- Story 3.5: Seat limit enforcement at token issuance
- Story 3.7: `GET /api/tenant/license` endpoint

**Priority 3 — Story bookkeeping:**
- Mark Epic 2 and Epic 5a stories "done" (code exists, just not marked)

### Diagnostic

- Read `4b-3-enriched-introspection-response-and-performance-gate.md` AC — confirm whether tenant-facing effective-permissions endpoint was expected there
- Read `src/OneId.Web/src/hooks/useHasPermission.ts` — confirm permission-gated UI works or is also mock

## Reproduction Plan

To observe the mock-data issue: navigate to any User detail page → Permissions tab → panel renders but data comes from `mockStore` not the backend. No API call to `/api/tenant/users/{userId}/effective-permissions` will appear in network tab.

---

## Follow-up: 2026-05-29

### New Evidence

**Finding 8: New user stepper — dimension step is a stub**

`src/OneId.Web/src/routes/tenant/users/new.tsx:167-176` — `StepDimensionAssignments` renders a static `DimensionalScopeSummary` with empty restrictions and the text "Dimension assignments can be configured after user creation." No API call, no selection UI. Step 3 of the stepper is non-functional.

**Finding 9: No dimension assignment hooks or pages exist anywhere**

Grep for `userDimension|addMember|removeMember|dimensions` across all frontend files returns only: `new.tsx`, `EffectivePermissions.tsx` (display only), `permissions/registry.*`, `mocks/fixtures.ts`. No `useUserDimensions`, `useSetUserDimensions`, `useAddGroupMember`, or `useRemoveGroupMember` hooks exist. The backend endpoints (`GET/POST/DELETE /api/tenant/users/{userId}/dimensions`, `PUT/DELETE /api/tenant/groups/{id}/members`) have zero frontend consumers outside of `new.tsx`'s group-assign-on-create.

**Finding 10: No user detail/edit page for existing users**

`routes/tenant/users/` contains only: `index.tsx`, `new.tsx`, `$userId/permissions.tsx`. There is no route or dialog to edit group memberships or dimension assignments for an existing user.

**Finding 11: CommandPalette entity search is on mock data**

`src/OneId.Web/src/components/shared/CommandPalette.tsx:96-152` — all three `EntitySearchAction` handlers call `mockStore.getUsers/getGroups/getRoles`. No API call.

**Finding 12: Internal admin `CreateUserDialog` silently drops selected groups**

`src/OneId.Server/routes/internal/tenants/TenantUsersPage.tsx:97-108` — `handleSubmit` calls `createUser.mutate({ email, displayName })` only. `selectedGroupIds` is collected in the UI but never passed to the mutation or followed by member-add calls.

**Finding 13: `atSeatLimit` hardcoded `false` in new user stepper**

`src/OneId.Web/src/routes/tenant/users/new.tsx:291` — `const atSeatLimit = false`. Seat limit gate in the stepper is permanently disabled.

**Finding 14: Backend group member endpoint is `PUT` (matches frontend)**

`TenantGroupsController.cs:96` — `[HttpPut("{id:guid}/members")]`. `new.tsx:332` — `apiClient.put(...)`. Confirmed match. ✓

### Complete Gap Table

| # | Gap | Location | Category |
|---|-----|----------|----------|
| M1 | `GET /api/tenant/users/{userId}/effective-permissions` backend endpoint missing | No controller | Missing backend |
| M2 | `POST /api/tenant/effective-permissions/preview` backend endpoint missing | No controller | Missing backend |
| M3 | `POST /api/tenant/users/{userId}/revoke-tokens` backend endpoint missing | No controller | Missing backend |
| M4 | `useAddGroupMember`, `useRemoveGroupMember`, `useUserDimensions` hooks missing | No hooks | Missing frontend |
| M5 | No UI to edit group memberships for existing users | No route | Missing frontend |
| M6 | Dimension assignment step 3 is a stub; no edit page anywhere | `new.tsx:167` | Missing frontend |
| M7 | CommandPalette entity search on mock data | `CommandPalette.tsx:96` | Mock data |
| M8 | EffectivePermissionsPanel live + preview on mock data | `features/users/api.ts:7` | Mock data |
| M9 | Internal admin create-user dialog drops group selection silently | `TenantUsersPage.tsx:97` | Bug |
| M10 | `atSeatLimit` hardcoded `false` in new user stepper | `new.tsx:291` | Bug |
| M11 | Epic 3 licensing (3.3, 3.5, 3.7) — not started | No story files | Planned Phase 6 |
