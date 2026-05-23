# Story 5c-1c: User Edit Dialog (Demo Scope)

Status: done

## Story

As an Internal Admin using the demo UI,
I want to edit an existing user's name, email, status, and group assignments from the Users list,
so that the Phase 5 UI demo milestone delivers the full "User management CRUD (create, edit, deactivate)" flow promised in the sprint change proposal.

## Acceptance Criteria

1. **Edit button in Users list** — Each user row in the Users list has an "Edit" button alongside the existing Activate/Deactivate button. Both buttons are disabled while any update mutation is pending.

2. **Edit dialog pre-filled** — Clicking "Edit" on a user opens a Dialog pre-filled with that user's current name, email, status (active/inactive toggle), and group assignments. The dialog title is "Edit User".

3. **Form fields** — The edit dialog contains the same four fields as the create dialog: name (required), email (required, must include `@`), status toggle (Active/Inactive), and group multi-select (same `GroupSelectList` component already in the file).

4. **Validation** — Validation fires on blur per field and on submit. Empty name shows "Name is required." Empty/invalid email shows "Email is required." / "Enter a valid email address." inline under the field.

5. **Submit** — Clicking "Save" calls `useUpdateUser.mutate({ userId, patch: { name, email, status, groupIds } })`. On success, dialog closes and user list refreshes. Submit button label changes to "Saving…" while pending.

6. **Stale-state prevention** — The edit dialog is conditionally rendered (`{editUser && <EditUserDialog … />}`) so it unmounts on close and remounts with fresh state when a different user is opened.

7. **Build clean** — `npm run build`, `npm run lint`, `npm test` all pass with no new errors.

## Tasks / Subtasks

- [x] Add `EditUserDialog` component to `TenantUsersPage.tsx` (AC: #2, #3, #4, #5)
- [x] Add `editUser` state and conditional render to `TenantUsersPage` page component (AC: #6)
- [x] Add "Edit" button to the actions column, update disabled logic (AC: #1)
- [x] Verify `npm run build`, `npm run lint`, `npm test` pass (AC: #7)

### Review Findings

- [x] [Review][Patch] No mutation error feedback — `updateUser.mutate` has no `onError` callback and no error state is displayed; a failed save leaves the dialog open and silent [`TenantUsersPage.tsx` — `EditUserDialog` `handleSubmit`]
- [x] [Review][Patch] Dialog's own `useUpdateUser` instance is separate from page-level — while dialog save is in-flight, the page-level `updateUser.isPending` stays `false`, so Edit and Activate/Deactivate row buttons remain enabled (AC 1 violation). Also: `useParams` called inside dialog instead of receiving `tenantId`/`updateUser` as props, inconsistent with `CreateUserDialog`. Fix: remove `useParams` + `useUpdateUser` from `EditUserDialog`, add `updateUser` and `tenantId` props passed from page. [`TenantUsersPage.tsx` — `EditUserDialog` + actions column]
- [x] [Review][Patch] `isOpen` prop always `true` — the conditional render `{editUser && …}` already controls visibility; passing `isOpen={true}` hardcoded is dead API surface that misleads future readers. Fix: remove the prop, pass `open={true}` directly to `<Dialog>`. [`TenantUsersPage.tsx` — `EditUserDialog` props + call site]
- [x] [Review][Patch] GroupSelectList checkbox IDs collide when both dialogs are mounted — `GroupSelectList` generates `id="user-grp-{groupId}"` for every checkbox; `CreateUserDialog` is always mounted and uses identical IDs; when `EditUserDialog` is also mounted the DOM has duplicate IDs, causing labels to associate with the wrong checkbox. Fix: add an `idPrefix` prop (default `"user-grp"`) to `GroupSelectList`; call site in `EditUserDialog` passes `idPrefix="edit-user-grp"`. [`TenantUsersPage.tsx` — `GroupSelectList` + `EditUserDialog`]
- [x] [Review][Defer] Stale `editUser` snapshot can clobber concurrent Activate/Deactivate status change — `editUser` state is set at click time; if Activate/Deactivate fires and query re-fetches while dialog is open, saving writes back the old status. Low practical risk in demo with mock data. — deferred, demo-scope limitation
- [x] [Review][Defer] `onOpenChange` silently blocks close while pending — Escape/backdrop close is swallowed with no visual feedback when save is in-flight; intentional per 5c-1b delete-dialog pattern, but leaves user confused. — deferred, pre-existing pattern
- [x] [Review][Defer] Weak email validation (`@` presence only) — pre-existing pattern from 5c-1b, deferred to Phase 2 real-auth wiring. — deferred, pre-existing
- [x] [Review][Defer] `user.groupIds` has no null-fallback — mock data always provides the array; same pattern as rest of codebase. — deferred, pre-existing

## Dev Notes

### CRITICAL: One file change only

**Only file to modify:** `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx`

No new hooks, no mock store changes, no new files. `useUpdateUser` already handles `patch: Partial<User>` — it works for both status-only toggles and full field edits.

---

### Current state of TenantUsersPage.tsx

The file already has:
- `GroupSelectList` component (lines 22–67) — **reuse as-is**, do not duplicate
- `CreateUserDialog` component (lines 70–198) — **model the EditUserDialog on this exactly**
- `useUpdateUser(tenantId)` imported and used for activate/deactivate toggle
- Actions column with a single Activate/Deactivate button

**What is missing:** an `EditUserDialog` component, `editUser` state, and an "Edit" button in the actions column.

---

### Step 1: Add `EditUserDialog` component

Add this component **between `CreateUserDialog` and the `TenantUsersPage` function**. It follows the same pattern as `CreateUserDialog` but:
- Accepts a `user: User` prop (the user being edited) instead of blank fields
- Initialises all `useState` from `user.*` — this works correctly because the component unmounts on close (conditional render, see Step 3)
- Calls `useUpdateUser` (not `useCreateUser`)
- Button label is "Save" / "Saving…" and title is "Edit User"

```typescript
function EditUserDialog({
  user,
  isOpen,
  onClose,
  groups,
}: {
  user: User
  isOpen: boolean
  onClose: () => void
  groups: Group[]
}) {
  const { tenantId = '' } = useParams<{ tenantId: string }>()
  const [name, setName] = useState(user.name)
  const [nameError, setNameError] = useState('')
  const [email, setEmail] = useState(user.email)
  const [emailError, setEmailError] = useState('')
  const [status, setStatus] = useState<'active' | 'inactive'>(user.status)
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>(user.groupIds)
  const updateUser = useUpdateUser(tenantId)

  const validateName = () => {
    if (!name.trim()) { setNameError('Name is required.'); return false }
    setNameError('')
    return true
  }

  const validateEmail = () => {
    if (!email.trim()) { setEmailError('Email is required.'); return false }
    if (!email.includes('@')) { setEmailError('Enter a valid email address.'); return false }
    setEmailError('')
    return true
  }

  const handleSubmit = () => {
    const nameOk = validateName()
    const emailOk = validateEmail()
    if (!nameOk || !emailOk) return
    updateUser.mutate(
      {
        userId: user.id,
        patch: { name: name.trim(), email: email.trim(), status, groupIds: selectedGroupIds },
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => { if (!open && !updateUser.isPending) onClose() }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Edit User</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="edit-user-name">Name</Label>
            <Input
              id="edit-user-name"
              value={name}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
              onBlur={validateName}
            />
            {nameError && <p className="text-sm text-destructive">{nameError}</p>}
          </div>
          <div className="space-y-1">
            <Label htmlFor="edit-user-email">Email</Label>
            <Input
              id="edit-user-email"
              type="email"
              value={email}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEmail(e.target.value)}
              onBlur={validateEmail}
            />
            {emailError && <p className="text-sm text-destructive">{emailError}</p>}
          </div>
          <div className="space-y-1">
            <Label>Status</Label>
            <div className="flex gap-2">
              <Button
                type="button"
                variant={status === 'active' ? 'default' : 'outline'}
                size="sm"
                onClick={() => setStatus('active')}
              >
                Active
              </Button>
              <Button
                type="button"
                variant={status === 'inactive' ? 'default' : 'outline'}
                size="sm"
                onClick={() => setStatus('inactive')}
              >
                Inactive
              </Button>
            </div>
          </div>
          <div className="space-y-1">
            <Label>Groups</Label>
            <GroupSelectList groups={groups} selected={selectedGroupIds} onChange={setSelectedGroupIds} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={updateUser.isPending}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={updateUser.isPending}>
            {updateUser.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
```

**`onOpenChange` guard** — uses `!updateUser.isPending` (same pattern as `_delete-dialog.tsx` fix from 5c-1b review) so Escape does not dismiss mid-save.

---

### Step 2: Add `editUser` state to page component

In `TenantUsersPage`, add alongside the existing `createOpen` state:

```typescript
const [editUser, setEditUser] = useState<User | null>(null)
```

At the bottom of the returned JSX, add the conditional render **after** `<CreateUserDialog …/>`:

```tsx
{editUser && (
  <EditUserDialog
    user={editUser}
    isOpen={true}
    onClose={() => setEditUser(null)}
    groups={groups}
  />
)}
```

`{editUser && …}` ensures the component unmounts when `editUser` is set to `null`, so the next open always gets fresh state initialised from the new user's data. Do NOT use `isOpen={!!editUser}` with a permanently mounted component — that was the stale-state bug fixed in 5c-1b review.

---

### Step 3: Update the actions column

Replace the current actions column cell (the single Activate/Deactivate button div) with:

```tsx
{
  id: 'actions',
  header: '',
  cell: ({ row }) => {
    const user = row.original
    const isActive = user.status === 'active'
    return (
      <div className="flex justify-end gap-2">
        <Button
          variant="outline"
          size="sm"
          disabled={updateUser.isPending}
          onClick={() => setEditUser(user)}
        >
          Edit
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={updateUser.isPending}
          onClick={() =>
            updateUser.mutate({
              userId: user.id,
              patch: { status: isActive ? 'inactive' : 'active' },
            })
          }
        >
          {isActive ? 'Deactivate' : 'Activate'}
        </Button>
      </div>
    )
  },
},
```

Both buttons share the same `updateUser.isPending` guard — they use the same hook instance and disabling both while either is in flight is correct (same pattern as all other pages in this codebase).

---

### ESLint design-token rule (do not break)

All `className` values must use semantic tokens only:
- `bg-background`, `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, `text-primary`, `text-destructive`
- Do NOT use raw Tailwind color classes (`text-red-500`, `bg-zinc-800`, etc.)

The `EditUserDialog` JSX above already follows this rule — copy it exactly.

---

### Imports — no changes needed

All required imports are already at the top of the file:
```typescript
import { useState } from 'react'
import { useParams } from 'react-router'
import { type ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { EmptyState } from '@/components/shared/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useUsers, useCreateUser, useUpdateUser, useGroups } from '@/queries/hooks'
import type { User, Group } from '@/mocks/types'
```

`User` type is already imported — `editUser` state is typed as `User | null`.

---

### Known limitations (acceptable for demo)

- Both Edit and Activate/Deactivate share the same `useUpdateUser` instance, so any row's action disables all rows' buttons while pending. This is the pre-existing pattern across all pages; acceptable for mock demo.
- Email validation is `@`-presence only — pre-existing pattern from 5c-1b, deferred to Phase 2 when real auth is wired.

---

### References

- `TenantUsersPage.tsx` current state: `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx`
- `useUpdateUser` hook: `src/OneId.Web/src/queries/hooks/useUsers.ts` — `mutate({ userId, patch: Partial<User> })`
- Stale-state fix pattern: [5c-1b review finding] "conditionally render the edit dialog instance so it unmounts on close"
- `onOpenChange` guard pattern: [5c-1b review finding] `_delete-dialog.tsx` — `if (!open && !isPending) onClose()`
- Sprint change proposal demo deliverable: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-23.md` §4 Phase 5

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None — implementation matched the story spec exactly.

### Completion Notes List

- Added `EditUserDialog` component between `CreateUserDialog` and the page function in `TenantUsersPage.tsx`. Uses conditional render `{editUser && <EditUserDialog />}` so component unmounts on close and remounts fresh on next open (stale-state fix from 5c-1b review).
- `EditUserDialog` initialises state from `user.*` props: name, email, status, groupIds. Reuses existing `GroupSelectList` component without duplication.
- `onOpenChange` guard uses `!updateUser.isPending` — Escape key does not dismiss mid-save.
- Actions column extended with "Edit" button (`gap-2` flex row). Both Edit and Activate/Deactivate share the single `updateUser` hook instance and its `isPending` flag.
- Build: clean (pre-existing chunk size warning, pre-existing DataTable warning — both unrelated to this story).
- Lint: 2 pre-existing errors in `TenantDetailPage.tsx` and `DataTable.tsx` — explicitly noted in 5c-1b dev notes as "do not fix". Zero new errors introduced.
- Tests: 38/38 passed.

### File List

- `src/OneId.Web/src/routes/internal/tenants/TenantUsersPage.tsx` — modified
