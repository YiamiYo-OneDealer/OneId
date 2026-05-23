# Story 5a.1: Design System Foundation

Status: done

## Story

As a developer,
I want a CSS token system, dark mode configuration, and typography scale enforced by an ESLint rule,
so that every component built from this point forward uses the design system automatically — no manual audits required.

## Acceptance Criteria

1. **CSS variable tokens** — `globals.css` (or `index.css`) defines 8 semantic tokens: `--background` (zinc-950), `--sidebar` (zinc-900), `--card` (zinc-800), `--popover` (zinc-800), `--primary` (indigo-500), `--destructive-fg` (red-500), `--destructive-bg` (red-950), `--admin-banner-bg` (amber-600). Tailwind is configured to use these as semantic aliases (e.g. `bg-background` maps to `var(--background)`).

2. **Dark mode** — Uses `class` strategy (not `media`). App defaults to dark mode on load: `<html class="dark">` is set before first paint (no flash of light theme). Inter typeface loaded and set as default font family.

3. **Typography scale** — 24px page title, 18px section heading, 14px body, 12px label/caption, 13px monospace for permission IDs. Tabular numerals enabled globally (`font-variant-numeric: tabular-nums`). Base unit 4px, primary rhythm 8px enforced via Tailwind spacing scale.

4. **ESLint rule** — Flags raw Tailwind color utilities on semantic JSX elements (e.g. `bg-zinc-950` directly instead of `bg-background`). `npm run lint` fails on violations. Rule does NOT flag raw colors in non-semantic contexts (SVG elements, test files).

## Tasks / Subtasks

- [x] Update `src/index.css` with correct semantic token values (AC: #1)
  - [x] Replace/update `.dark` class HSL values to match zinc/indigo/red/amber palette
  - [x] Add `--sidebar`, `--destructive-fg`, `--destructive-bg`, `--admin-banner-bg` custom tokens
  - [x] Extend `@theme inline` block to expose new tokens as Tailwind utilities
- [x] Add Inter font (AC: #2)
  - [x] `npm install @fontsource-variable/inter` in `src/OneId.Web/`
  - [x] Import in `src/main.tsx` or `src/index.css`
  - [x] Set `font-family: 'InterVariable', sans-serif` on `:root`/`body`
- [x] Set dark mode default (AC: #2)
  - [x] Add `class="dark"` to `<html>` in `index.html` before first paint
- [x] Configure typography scale (AC: #3)
  - [x] Define custom `--text-*` tokens in `@theme` or apply via body/global CSS
  - [x] Enable `font-variant-numeric: tabular-nums` globally
- [x] Add ESLint custom rule (AC: #4)
  - [x] Write inline custom ESLint rule in `eslint.config.js` (flat config format)
  - [x] Rule flags `bg-zinc-950`, `bg-zinc-900`, `bg-zinc-800`, `bg-indigo-500`, `text-indigo-500`, `bg-red-500`, `text-red-500`, `bg-red-950`, `bg-amber-600`, `text-indigo-300` in JSX `className` props
  - [x] Exclude: SVG elements (`<svg>`, `<path>`, `<circle>`, etc.) and `*.test.tsx` / `*.spec.tsx` files
- [x] Clean up `src/App.css` (remove Vite default template styles that conflict with design system)
- [x] Verify `npm run lint` passes and `npm run build` produces no TypeScript errors

## Dev Notes

### Current Project State (READ BEFORE TOUCHING ANYTHING)

**Tailwind version: v4** — This is CRITICAL. The project uses `tailwindcss@^4.3.0` with `@tailwindcss/vite`. There is **no `tailwind.config.js`**. All Tailwind theme configuration is done via CSS `@theme inline` and `@theme` blocks inside `index.css`. Do NOT create a `tailwind.config.ts` or `tailwind.config.js` — it would be ignored and break the build.

**shadcn already initialized** — `components.json` is present, style is "new-york", base color "zinc", CSS variables enabled. shadcn components depend on the existing `--background`, `--foreground`, `--card`, `--card-foreground`, `--popover`, `--popover-foreground`, `--primary`, `--primary-foreground`, `--destructive`, `--destructive-foreground`, `--border`, `--input`, `--ring`, `--radius` token names. **Do not rename these tokens** — only update their HSL values. Adding new tokens alongside is safe.

**Existing `src/index.css`** — Has the full shadcn default token block (HSL-based). A TODO comment at line 85 explicitly marks this file for Story 5a.1 changes. The `.dark` class already has token values — update them. The `@theme inline` block at the top must be preserved and extended.

**ESLint: flat config** — `eslint.config.js` uses the new ESLint flat config format (not `.eslintrc`). Any custom rule must be written inline as a plugin object in the flat config array. See flat config plugin format below.

**Files to modify:**
- `src/OneId.Web/src/index.css` — main CSS changes
- `src/OneId.Web/index.html` — add `class="dark"` to `<html>`
- `src/OneId.Web/src/main.tsx` — add Inter font import
- `src/OneId.Web/eslint.config.js` — add custom ESLint rule
- `src/OneId.Web/src/App.css` — clear/reduce (Vite default junk; remove anything that conflicts)

**No `playwright.config.ts` changes needed** — already created in Story 1.6 with `--force-color-profile=srgb`. Story 5a.5 will extend it.

### CSS Token Implementation

**Color values (HSL format, for `.dark` class):**

| Token | Tailwind Color | HSL Value |
|-------|---------------|-----------|
| `--background` | zinc-950 | `240 5.9% 3.9%` |
| `--sidebar` | zinc-900 | `240 4.9% 10.4%` ← NEW |
| `--card` | zinc-800 | `240 3.7% 15.9%` |
| `--popover` | zinc-800 | `240 3.7% 15.9%` |
| `--primary` | indigo-500 | `239 84% 67.1%` |
| `--destructive` | red-500 | `0 84.2% 60.2%` (keep for shadcn compat) |
| `--destructive-fg` | red-500 | `0 84.2% 60.2%` ← NEW |
| `--destructive-bg` | red-950 | `0 72.2% 16.5%` ← NEW |
| `--admin-banner-bg` | amber-600 | `38 92.3% 47.8%` ← NEW |

For foreground tokens that pair with the above (needed by shadcn):
- `--primary-foreground`: `0 0% 100%` (white text on indigo)
- `--destructive-foreground`: `0 0% 98%` (near-white text on red button)
- `--foreground`: `0 0% 98%` (near-white body text)
- `--card-foreground` / `--popover-foreground`: `0 0% 98%`

Also update other `.dark` tokens that affect look:
- `--secondary`: `240 3.7% 15.9%` (zinc-800)
- `--muted`: `240 3.7% 15.9%` (zinc-800)
- `--muted-foreground`: `240 5% 64.9%` (zinc-400)
- `--accent`: `240 3.7% 15.9%` (zinc-800)
- `--border`: `240 3.7% 20%` (slightly lighter zinc)
- `--input`: `240 3.7% 20%`

**Extending `@theme inline`** — Add custom tokens so Tailwind utilities work:
```css
@theme inline {
  /* existing tokens preserved ... */
  --color-sidebar: hsl(var(--sidebar));
  --color-destructive-fg: hsl(var(--destructive-fg));
  --color-destructive-bg: hsl(var(--destructive-bg));
  --color-admin-banner-bg: hsl(var(--admin-banner-bg));
}
```
This enables `bg-sidebar`, `bg-destructive-fg`, `bg-destructive-bg`, `bg-admin-banner-bg` as Tailwind utility classes.

**IMPORTANT**: The `:root` light-mode tokens can be kept as-is or match the dark values — since the app always uses dark mode (`class="dark"` on `<html>`), the `:root` values are never active in production. Keep them for shadcn CLI compatibility but they won't be visible.

### Inter Font — Use Variable Font Package

Install the variable font package (supports all weights, single import, no CDN needed):
```bash
cd src/OneId.Web
npm install @fontsource-variable/inter
```

**Implementation note**: Import via CSS `@import` in `index.css` (NOT as a TypeScript side-effect import in `main.tsx`). TypeScript strict mode rejects the side-effect import since the package has no type declarations. CSS imports are processed by Vite/PostCSS without TypeScript involvement.

```css
/* top of index.css, before @import "tailwindcss" */
@import "@fontsource-variable/inter";
```

In `index.css` (inside `@layer base`):
```css
body {
  font-family: 'InterVariable', 'Inter', sans-serif;
}
```

Do NOT use Google Fonts CDN — this console runs in CI and potentially air-gapped environments. The npm package is the correct approach.

### Dark Mode — Before First Paint

Add `class="dark"` directly to `index.html`. This is the correct approach to avoid FOUC (flash of unstyled content):
```html
<html lang="en" class="dark">
```

No JavaScript `localStorage` check needed in this story — the app is always dark mode. A future story could add a theme toggle; for now, hardcode dark.

Tailwind v4 uses the `class` strategy by default when you have `.dark { }` selectors in CSS. No additional Tailwind config needed.

### Typography Scale

Define in `@layer base` inside `index.css`:
```css
@layer base {
  :root {
    /* Typography scale */
    --font-size-page-title: 1.5rem;      /* 24px */
    --font-size-section-heading: 1.125rem; /* 18px */
    --font-size-body: 0.875rem;            /* 14px */
    --font-size-caption: 0.75rem;          /* 12px */
    --font-size-mono: 0.8125rem;           /* 13px */
  }

  body {
    font-size: var(--font-size-body);
    font-variant-numeric: tabular-nums;
    font-family: 'InterVariable', 'Inter', sans-serif;
  }
}
```

Tailwind's default spacing scale already uses 4px base (1 unit = 4px). No override needed — just document that `p-2` = 8px, `p-4` = 16px, etc. in the Dev notes.

### ESLint Rule — Flat Config Format

The rule must be written in `eslint.config.js` using the flat config plugin format. The raw colors to protect are those mapped to the 8 semantic tokens:

```js
// In eslint.config.js, add this to the config array:
{
  files: ['**/*.{ts,tsx}'],
  ignores: ['**/*.test.tsx', '**/*.spec.tsx', '**/*.test.ts', '**/*.spec.ts'],
  plugins: {
    'design-tokens': {
      rules: {
        'no-raw-color-on-semantic-element': {
          create(context) {
            const PROTECTED_COLORS = [
              'bg-zinc-950', 'text-zinc-950',
              'bg-zinc-900', 'text-zinc-900',
              'bg-zinc-800', 'text-zinc-800',
              'bg-indigo-500', 'text-indigo-500',
              'bg-red-500', 'text-red-500',
              'bg-red-950', 'text-red-950',
              'bg-amber-600', 'text-amber-600',
              'text-indigo-300',
            ]
            const SVG_ELEMENTS = new Set(['svg', 'path', 'circle', 'rect', 'line', 'polygon', 'ellipse', 'g'])

            function checkClassName(node, classValue) {
              if (typeof classValue !== 'string') return
              const found = PROTECTED_COLORS.filter(c => classValue.includes(c))
              if (found.length > 0) {
                context.report({
                  node,
                  message: `Use CSS variable token alias instead of raw Tailwind color utility on semantic elements: ${found.join(', ')}`,
                })
              }
            }

            return {
              JSXAttribute(node) {
                if (node.name.name !== 'className') return
                // Skip SVG elements
                const parent = node.parent
                if (parent && parent.name && SVG_ELEMENTS.has(parent.name.name)) return
                // Check string literals
                if (node.value?.type === 'Literal') {
                  checkClassName(node, node.value.value)
                }
                // Check JSX expression containers with string literals
                if (node.value?.type === 'JSXExpressionContainer') {
                  const expr = node.value.expression
                  if (expr?.type === 'Literal') {
                    checkClassName(node, expr.value)
                  }
                }
              },
            }
          },
        },
      },
    },
  },
  rules: {
    'design-tokens/no-raw-color-on-semantic-element': 'error',
  },
},
```

**Known limitation**: The rule only catches string literals in `className`. It does NOT catch `clsx()`, `cn()`, or template literals — that's acceptable for Phase 1. The rule prevents accidental raw color usage in direct `className` strings.

### App.css Cleanup

`src/App.css` cleared — file is now empty. The Vite default styles (`.counter`, `.hero`, `#center`, etc.) are irrelevant to the design system.

### Testing Requirements

This story has no dedicated test file — validation is through:
1. `npm run lint` — must exit 0, ESLint rule must be active
2. `npm run build` — must exit 0, zero TypeScript errors
3. Visual check: open `http://localhost:5173` → page should render in dark mode (dark background, no flash)
4. Lint violation test: temporarily add `className="bg-zinc-950"` to any JSX element and verify `npm run lint` fails; then revert

No `vitest` component tests in this story — those are introduced in Story 5a.4.

### Project Structure Notes

All changes are inside `src/OneId.Web/`. The backend (`src/OneId.Server/`) is untouched.

Path conventions for future stories:
- CSS tokens live in `src/OneId.Web/src/index.css` (the shadcn CSS entry point)
- There is NO separate `globals.css` file — the story AC says "globals.css is created" but since shadcn uses `index.css` as its CSS entry point (configured in `components.json`), use `index.css` as the globals file. Do not create a separate `globals.css` as it would require changing the shadcn config.
- Tailwind utilities generated from `@theme inline` in `index.css` are available project-wide
- The `@/` alias maps to `src/` — use `@/components`, `@/hooks`, `@/lib`, `@/features`, etc.
- Inter font imported via CSS `@import` in `index.css` (not TypeScript side-effect import — no type declarations available)

### References

- Epic 5a story requirements: [epics.md § Story 5a.1](../_bmad-output/planning-artifacts/epics.md)
- UX token specification: [ux-design-specification.md § Design System Foundation](../_bmad-output/planning-artifacts/ux-design-specification.md)
- UX-DR1: CSS token system, 8 tokens, ESLint rule enforcement
- UX-DR2: Dark mode `dark:` variant, Inter typeface, type scale, tabular numerals
- Architecture: Tailwind CSS via shadcn/ui, Vite 6, React 19 TypeScript strict [architecture.md § Frontend Architecture]
- shadcn/ui config: [src/OneId.Web/components.json](../../src/OneId.Web/components.json)
- Current ESLint config: [src/OneId.Web/eslint.config.js](../../src/OneId.Web/eslint.config.js) — flat config format
- Current CSS entry: [src/OneId.Web/src/index.css](../../src/OneId.Web/src/index.css)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6 (create-story workflow, 2026-05-23)
claude-sonnet-4-6 (dev-story implementation, 2026-05-23)

### Debug Log References

- Inter font: TypeScript side-effect import of `@fontsource-variable/inter` fails with TS2882 (no type declarations). Fixed by moving import to CSS `@import` in `index.css` instead — Vite processes this without TypeScript involvement.

### Completion Notes List

- All 8 semantic tokens implemented in `index.css` `.dark` block with correct HSL values
- `@theme inline` extended with 4 new OneId tokens: `--color-sidebar`, `--color-destructive-fg`, `--color-destructive-bg`, `--color-admin-banner-bg`
- Typography scale CSS variables defined in `:root`, applied to `body`
- `font-variant-numeric: tabular-nums` set globally on body
- Inter variable font installed via npm, imported via CSS `@import` (not TS side-effect import)
- `index.html` updated: `<html lang="en" class="dark">` — dark mode active before first paint
- ESLint flat config extended with inline `design-tokens` plugin rule — catches raw protected colors in JSX `className` props, excludes SVG elements and test files
- `App.css` cleared of all Vite default styles
- `npm run lint` exits 0; `npm run build` exits 0 (Inter font woff2 files bundled into dist)
- Lint violation test verified: adding `className="bg-zinc-950"` to JSX triggers error `design-tokens/no-raw-color-on-semantic-element`

### File List

- `src/OneId.Web/src/index.css` — MODIFIED: design system tokens, dark mode, typography scale, Inter font import
- `src/OneId.Web/index.html` — MODIFIED: added `class="dark"` to `<html>`
- `src/OneId.Web/eslint.config.js` — MODIFIED: added design-tokens ESLint plugin rule
- `src/OneId.Web/src/App.css` — MODIFIED: cleared (was Vite default styles)
- `src/OneId.Web/package.json` — MODIFIED: added `@fontsource-variable/inter` dependency
