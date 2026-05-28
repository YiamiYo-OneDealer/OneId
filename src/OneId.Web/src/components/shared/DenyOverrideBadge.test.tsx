import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, describe, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { DenyOverrideBadge } from './DenyOverrideBadge'

describe('DenyOverrideBadge', () => {
  it('non-interactive state — no axe violations', async () => {
    const { container } = render(
      <DenyOverrideBadge permissionLabel="Deactivate Users" />,
    )
    expect(await axe(container)).toHaveNoViolations()
  })

  it('interactive state — no axe violations', async () => {
    const { container } = render(
      <DenyOverrideBadge permissionLabel="Deactivate Users" onReview={() => {}} />,
    )
    expect(await axe(container)).toHaveNoViolations()
  })

  it('non-interactive: renders DENY label with role=status', () => {
    render(<DenyOverrideBadge permissionLabel="Deactivate Users" />)
    const badge = screen.getByRole('status')
    expect(badge).toHaveTextContent('DENY')
    expect(badge).toHaveAttribute('aria-label', 'DENY override on Deactivate Users')
  })

  it('interactive: renders as button with correct aria-label', () => {
    render(<DenyOverrideBadge permissionLabel="Deactivate Users" onReview={() => {}} />)
    const btn = screen.getByRole('button', { name: /DENY override on Deactivate Users/i })
    expect(btn).toHaveTextContent('DENY')
  })

  it('interactive: calls onReview when clicked', async () => {
    const user = userEvent.setup()
    const onReview = vi.fn()
    render(<DenyOverrideBadge permissionLabel="Deactivate Users" onReview={onReview} />)
    await user.click(screen.getByRole('button'))
    expect(onReview).toHaveBeenCalledOnce()
  })
})
