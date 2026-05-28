import { render, screen } from '@testing-library/react'
import { expect, describe, it } from 'vitest'
import { axe } from 'vitest-axe'
import { DisabledButtonWithTooltip } from './DisabledButtonWithTooltip'

describe('DisabledButtonWithTooltip', () => {
  it('permission-block state — no axe violations', async () => {
    const { container } = render(
      <DisabledButtonWithTooltip tooltip="You don't have permission to create roles. Contact your administrator.">
        <button>Create Role</button>
      </DisabledButtonWithTooltip>
    )
    expect(await axe(container)).toHaveNoViolations()
  })

  it('precondition-block state — no axe violations', async () => {
    const { container } = render(
      <DisabledButtonWithTooltip tooltip="No roles assigned to this group. Add a role first.">
        <button>Assign Users</button>
      </DisabledButtonWithTooltip>
    )
    expect(await axe(container)).toHaveNoViolations()
  })

  it('button has aria-disabled and aria-describedby', () => {
    render(
      <DisabledButtonWithTooltip tooltip="You don't have permission to delete roles. Contact your administrator.">
        <button>Delete Role</button>
      </DisabledButtonWithTooltip>
    )
    const btn = screen.getByRole('button', { name: 'Delete Role' })
    expect(btn).toBeDisabled()
    expect(btn).toHaveAttribute('aria-disabled', 'true')
    expect(btn).toHaveAttribute('aria-describedby')
  })

  it('keyboard-only: wrapper span is focusable', () => {
    const { container } = render(
      <DisabledButtonWithTooltip tooltip="You don't have permission to create roles. Contact your administrator.">
        <button>Create Role</button>
      </DisabledButtonWithTooltip>
    )
    const span = container.querySelector('span[tabindex="0"]')
    expect(span).not.toBeNull()
  })
})
