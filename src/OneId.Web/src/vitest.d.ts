/* eslint-disable @typescript-eslint/no-empty-object-type, @typescript-eslint/no-unused-vars */
import 'vitest'

interface CustomMatchers {
  toHaveNoViolations(): void
}

declare module 'vitest' {
  interface Assertion<T = unknown> extends CustomMatchers {}
  interface AsymmetricMatchersContaining extends CustomMatchers {}
}
