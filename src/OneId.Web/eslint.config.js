import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
  },
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
                  const parent = node.parent
                  if (parent && parent.name && SVG_ELEMENTS.has(parent.name.name)) return
                  if (node.value?.type === 'Literal') {
                    checkClassName(node, node.value.value)
                  }
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
])
