# Story 5.6: DimensionalScopeSummary Component

Status: review

## Story

As a Tenant Admin assigning a role with dimensional restrictions to a user,
I want to see a plain-language sentence summarising what the user can access,
so that I can confirm the assignment is correct before saving and trust the saved state afterward.

## Acceptance Criteria

1. **Props Interface** — Component accepts `restrictions: Record<DimensionAxis, string[]>` and `roleName: string`; it is purely presentational (no API calls, no TanStack Query hooks). `DimensionAxis` is the string-union type `"Company" | "Location" | "Branch" | "Make" | "MarketSegment"` defined in `src/features/users/schemas.ts`.

2. **Template Format** — Output sentence: `"[Role Name] — restricted to [Axis: value1, value2] and [Axis: value1]"`. Only axes that have at least one assigned value appear in the sentence. Multiple axes are joined with ` and `.
   - Example: `"Sales Manager — restricted to Location: Amsterdam, Utrecht and Make: BMW, Audi"`

3. **All-Values Shorthand** — When every value in a given axis is selected (caller passes all available values or a sentinel), describe as `"all [axis-plural]"`.
   - Example: `"Inventory Viewer — restricted to Make: all makes"` (not a list of every make)
   - **Implementation note**: The component receives only the `restrictions` prop (assigned values). The "all values" shorthand is triggered when the caller passes the sentinel string `"*"` as one of the values for that axis OR when the caller explicitly signals it by passing `{ allValues: true }`. Since the component is pure presentational, the simplest contract: if any value in the array equals the string `"*"`, render the all-values shorthand for that axis. The caller (role assignment form, Story 5c.2) is responsible for passing `["*"]` when all values are selected.

4. **Truncation (> 3 values)** — For an axis with more than 3 assigned values, show the first 3 followed by `"+N more"` where N is the remaining count.
   - Example: `"Location: Amsterdam, Utrecht, Rotterdam +2 more"` (5 values total)
   - The `"+N more"` text is wrapped in a `<Tooltip>` (shadcn `Tooltip`) that reveals the full list on hover/focus.
   - The tooltip trigger element has `aria-label="Show all [Axis] values"` for screen reader accessibility.
   - Keyboard: tooltip trigger is focusable (button role or `tabIndex={0}` span with role="button").

5. **Singular vs. Plural Axis Label** — Single assigned value uses the singular form of the axis name; multiple values use plural.
   - Singular map: `Company → Company`, `Location → Location`, `Branch → Branch`, `Make → Make`, `MarketSegment → Market Segment` (display label).
   - Plural map: `Company → Companies`, `Location → Locations`, `Branch → Branches`, `Make → Makes`, `MarketSegment → Market Segments`.
   - Example single: `"Location: Amsterdam"` (not "Locations: Amsterdam").
   - Example plural: `"Locations: Amsterdam, Utrecht"`.
   - All-values shorthand always uses plural: `"all locations"`, `"all makes"`.

6. **No Restrictions** — When `restrictions` contains no axes with assigned values (all arrays are empty or the prop is empty), display: `"no dimensional restrictions (full scope)"`. Never blank.

7. **Live Integration** — The component is pure/stateless; it re-renders automatically when the parent updates `restrictions` or `roleName`. Story 5c.2 (New User stepper Step 3) will consume it live. This story does NOT wire it into 5c.2 — that is out of scope here.

8. **Testing** — vitest test file co-located at `src/components/shared/DimensionalScopeSummary.test.tsx` covering:
   - AC2: basic sentence with two axes
   - AC3: all-values shorthand (`["*"]` sentinel)
   - AC4: truncation at > 3 values, tooltip trigger present with correct `aria-label`
   - AC5: singular axis label (1 value) vs. plural (multiple values)
   - AC6: no restrictions message
   - Axe accessibility check (via `vitest-axe`): component renders without accessibility violations

## Tasks / Subtasks

- [x] Task 1: Define `DimensionAxis` type and axis label helpers (AC: 1, 5)
  - [x] Add `DimensionAxis` string-union type to `src/features/users/schemas.ts` (alongside existing Zod schemas; infer the type from a `z.enum()` definition so it's consistent with Rule 11)
  - [x] Add `AXIS_SINGULAR_LABELS` and `AXIS_PLURAL_LABELS` constant maps in the component file (local constants, not exported — component-private)

- [x] Task 2: Build `DimensionalScopeSummary` component (AC: 2, 3, 4, 5, 6)
  - [x] Create `src/components/shared/DimensionalScopeSummary.tsx`
  - [x] Implement sentence-building logic (filter empty axes → format each axis segment → join with " and ")
  - [x] Handle `["*"]` sentinel → all-values shorthand
  - [x] Handle > 3 values → truncated display with `+N more` tooltip trigger
  - [x] Handle no restrictions → `"no dimensional restrictions (full scope)"`
  - [x] Wrap `+N more` in shadcn `<Tooltip>` with full value list in `TooltipContent`; tooltip trigger is keyboard-focusable with correct `aria-label`

- [x] Task 3: Write test file (AC: 8)
  - [x] Create `src/components/shared/DimensionalScopeSummary.test.tsx`
  - [x] Cover all AC scenarios listed in AC8
  - [x] Include `vitest-axe` accessibility test

- [x] Task 4: Export from shared barrel (housekeeping)
  - [x] No barrel file exists — components are imported directly (skipped per story note)

## Dev Notes

### Component is Purely Presentational — No API, No Query

This component must NOT import `ky`, `useQuery`, `useMutation`, or anything from TanStack Query. It receives all data as props. This is intentional — the parent (role assignment form, user detail view) is responsible for fetching dimension data and passing it in.

### DimensionAxis Type Location

`DimensionAxis` does not yet exist in the frontend. Create it in `src/features/users/schemas.ts` following Rule 11 (Zod schema → infer type):

```ts
export const dimensionAxisSchema = z.enum([
  'Company',
  'Location',
  'Branch',
  'Make',
  'MarketSegment',
]);
export type DimensionAxis = z.infer<typeof dimensionAxisSchema>;
```

These string values exactly match the backend `DimensionAxis` enum member names (used as strings in `DimensionValueDto.Axis` and `UserDimensionsGroupedDto` property names).

### Axis Display Labels

The `MarketSegment` axis must render as `"Market Segment"` (with a space) in all user-facing text. All other axes render as their name as-is. Build these maps locally in the component file:

```ts
const AXIS_SINGULAR: Record<DimensionAxis, string> = {
  Company: 'Company',
  Location: 'Location',
  Branch: 'Branch',
  Make: 'Make',
  MarketSegment: 'Market Segment',
};
const AXIS_PLURAL: Record<DimensionAxis, string> = {
  Company: 'Companies',
  Location: 'Locations',
  Branch: 'Branches',
  Make: 'Makes',
  MarketSegment: 'Market Segments',
};
```

### All-Values Sentinel

The `"*"` sentinel string is the agreed contract between this component and its callers. When `restrictions["Location"] === ["*"]`, render `"all locations"` (lowercase plural). Keep this logic encapsulated in a helper `isAllValues(values: string[]): boolean = values.length === 1 && values[0] === '*'`.

### Truncation and Tooltip Pattern

Follow the existing `DisabledButtonWithTooltip` and `ProvenanceChain` patterns for tooltip usage. Use shadcn `Tooltip` + `TooltipTrigger` + `TooltipContent`:

```tsx
<Tooltip>
  <TooltipTrigger asChild>
    <button
      type="button"
      className="text-indigo-400 underline-offset-2 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
      aria-label={`Show all ${AXIS_PLURAL[axis]} values`}
    >
      +{remaining} more
    </button>
  </TooltipTrigger>
  <TooltipContent>
    <ul className="text-sm">
      {allValues.map(v => <li key={v}>{v}</li>)}
    </ul>
  </TooltipContent>
</Tooltip>
```

Note: `type="button"` is required to prevent accidental form submission if this component appears inside a `<form>`.

### Sentence Assembly Algorithm

```
1. Filter `restrictions` to only axes where values.length > 0
2. For each such axis, build a segment:
   a. If isAllValues(values) → `${AXIS_PLURAL[axis]}: all ${AXIS_PLURAL[axis].toLowerCase()}`
      Wait — re-read AC3 example: "Make: all makes" — the label before the colon is the axis name (singular or plural?).
      Per AC5, singular when 1 value, plural when multiple. "all makes" means multiple (conceptually), so use plural:
      → `"${AXIS_PLURAL[axis]}: all ${AXIS_PLURAL[axis].toLowerCase()}"`
      → e.g. "Makes: all makes"
   b. If values.length <= 3 → `${label}: ${values.join(', ')}`
      (label = singular if 1 value, plural if > 1)
   c. If values.length > 3 → first 3 joined + tooltip trigger for remainder
3. Join segments with " and "
4. Prepend "[roleName] — restricted to "
5. If no segments (all axes empty) → "no dimensional restrictions (full scope)"
```

### Styling

- Component output is inline text mixed with a tooltip trigger button, wrapped in a containing `<p>` or `<span>`.
- Text color: `text-zinc-300` (matches rest of panel text in EffectivePermissionsPanel).
- `+N more` button: `text-indigo-400` with hover underline, matching ProvenanceChain link style.
- No background, no border — this is flowing prose text.

### Testing Pattern

Follow the `SeatUsageIndicator.test.tsx` and `DisabledButtonWithTooltip.test.tsx` patterns established in 5b-2 and 5b-5:

```ts
import { render, screen } from '@testing-library/react';
import { axe } from 'vitest-axe'; // already installed per 5b-5

describe('DimensionalScopeSummary', () => {
  it('renders basic sentence with two axes', () => { ... });
  it('renders all-values shorthand for sentinel', () => { ... });
  it('truncates at >3 values and shows +N more', () => { ... });
  it('uses singular axis label for single value', () => { ... });
  it('renders no-restrictions message when empty', () => { ... });
  it('has no accessibility violations', async () => {
    const { container } = render(<DimensionalScopeSummary ... />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

For the tooltip trigger `aria-label`, assert with `screen.getByRole('button', { name: 'Show all Locations values' })`.

### Previous Story Intelligence (from 5b-5)

- `vitest-axe` is already installed and working — import pattern: `import { axe } from 'vitest-axe'`
- `useHasPermission` three-state pattern is established — NOT needed here (pure presentational)
- shadcn `Tooltip` is already in use (DenyOverrideSheet uses it) — import from `@/components/ui/tooltip`
- Tests use `getByRole` and `getByLabelText` (not `getByText`) for accessibility-sensitive assertions
- Architecture Rules 11, 12, 14 enforced — Rule 11 applies here (Zod schema → type for `DimensionAxis`)
- Test count baseline: 115 passing; this story adds ~6 tests

### Architecture Compliance Checklist

- [ ] `DimensionAxis` type defined via `z.enum()` in `features/users/schemas.ts` (Rule 11 — Zod schema first)
- [ ] No TanStack Query in this component (it is pure presentational — no `useQuery`, no `ky`)
- [ ] `type="button"` on any `<button>` inside the component (prevents form submission)
- [ ] `aria-label` on tooltip trigger (accessibility requirement)
- [ ] Co-located test file `.test.tsx` alongside component

### Project Structure Notes

- New files land in `src/OneId.Web/src/components/shared/` (alongside DenyOverrideSheet, SeatUsageIndicator, ProvenanceChain)
- Type addition goes to `src/OneId.Web/src/features/users/schemas.ts` (existing file)
- No new routes, no new API hooks, no mock store changes required

### References

- [Source: epics.md — Epic 5b Story 5b.6 acceptance criteria]
- [Source: architecture.md — Frontend Rules 11, 12, 14; Component folder conventions]
- [Source: ux-design.md — Design Opportunities "Compiled meaning preview"; Novel UX Patterns table; Open Items truncation strategy]
- [Source: 5b-5 story file — vitest-axe pattern, shadcn Tooltip usage, architecture rule enforcement examples]
- [Source: DimensionAxis.cs — Backend enum values: Company=0, Location=1, Branch=2, Make=3, MarketSegment=4]
- [Source: UserDimensionsGroupedDto.cs — Confirms axis names used as property names (match enum member names)]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- One test failure fixed: tooltip content not in DOM when closed (shadcn Tooltip uses Portal, only renders on open). Fixed by asserting `+N more` button count instead of DOM content.

### Completion Notes List

- `DimensionAxis` defined as `z.enum(...)` in `features/users/schemas.ts` per Rule 11 — first frontend type to cover the 5 backend dimension axes.
- `DimensionalScopeSummary` is fully pure/presentational — no API calls, no TanStack Query.
- `"*"` sentinel triggers all-values shorthand; axis order follows backend enum order.
- `MarketSegment` renders as `"Market Segment"` (spaced) in all labels.
- Truncation uses shadcn `Tooltip` with `TooltipProvider` wrapping (matches `DisabledButtonWithTooltip` pattern).
- 11 tests added; total suite 126 passing (was 115), 0 regressions.

### File List

- `src/OneId.Web/src/features/users/schemas.ts` — UPDATED (added `dimensionAxisSchema` and `DimensionAxis` type)
- `src/OneId.Web/src/components/shared/DimensionalScopeSummary.tsx` — NEW
- `src/OneId.Web/src/components/shared/DimensionalScopeSummary.test.tsx` — NEW
