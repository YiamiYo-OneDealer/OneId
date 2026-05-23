import '@testing-library/jest-dom'
import { expect } from 'vitest'

// Custom axe matcher — vitest-axe@0.1.0 ships only `axe`/`configureAxe`, not a matcher.
// `toHaveNoViolations` is registered here so all test files get it via the setup file.
expect.extend({
  toHaveNoViolations(received: { violations: { id: string; description: string }[] }) {
    const pass = received.violations.length === 0
    return {
      pass,
      message: () =>
        pass
          ? 'Expected axe violations to be present, but none were found'
          : `Expected no axe violations but found ${received.violations.length}:\n${received.violations.map((v) => `  [${v.id}] ${v.description}`).join('\n')}`,
    }
  },
})
