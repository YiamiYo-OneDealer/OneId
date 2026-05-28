import { render, screen } from '@testing-library/react'
import { expect, describe, it } from 'vitest'
import { axe } from 'vitest-axe'
import { SeatUsageIndicator, isSeatLimitReached } from './SeatUsageIndicator'

describe('SeatUsageIndicator', () => {
  it('normal state (<80%): shows zinc text, no icon', () => {
    const { container } = render(<SeatUsageIndicator used={8} max={25} />)
    const el = screen.getByLabelText('8 of 25 seats used')
    expect(el).toHaveClass('text-zinc-400')
    expect(container.querySelector('[aria-hidden="true"]')).toBeNull()
  })

  it('warning state (≥80%, <100%): shows amber text and warning icon', () => {
    const { container } = render(<SeatUsageIndicator used={20} max={25} />)
    const el = screen.getByLabelText('20 of 25 seats used')
    expect(el).toHaveClass('text-amber-400')
    expect(container.querySelector('[aria-hidden="true"]')).not.toBeNull()
  })

  it('limit-reached state (100%): shows red text and alert icon', () => {
    const { container } = render(<SeatUsageIndicator used={25} max={25} />)
    const el = screen.getByLabelText('25 of 25 seats used')
    expect(el).toHaveClass('text-red-400')
    expect(container.querySelector('[aria-hidden="true"]')).not.toBeNull()
  })

  it('unlimited (max=null): shows "N seats used" with zinc text', () => {
    render(<SeatUsageIndicator used={42} max={null} />)
    const el = screen.getByLabelText('42 seats used')
    expect(el).toHaveClass('text-zinc-400')
  })

  it('screen-reader label uses full words not slash notation', () => {
    render(<SeatUsageIndicator used={8} max={25} />)
    const indicator = screen.getByLabelText('8 of 25 seats used')
    expect(indicator).toHaveAttribute('aria-label', '8 of 25 seats used')
  })

  it('isSeatLimitReached: true when used >= max', () => {
    expect(isSeatLimitReached(25, 25)).toBe(true)
    expect(isSeatLimitReached(26, 25)).toBe(true)
  })

  it('isSeatLimitReached: false when used < max or max is null', () => {
    expect(isSeatLimitReached(24, 25)).toBe(false)
    expect(isSeatLimitReached(100, null)).toBe(false)
  })

  it('axe: no violations in normal state', async () => {
    const { container } = render(<SeatUsageIndicator used={8} max={25} />)
    expect(await axe(container)).toHaveNoViolations()
  })

  it('axe: no violations in warning state', async () => {
    const { container } = render(<SeatUsageIndicator used={20} max={25} />)
    expect(await axe(container)).toHaveNoViolations()
  })

  it('axe: no violations in limit-reached state', async () => {
    const { container } = render(<SeatUsageIndicator used={25} max={25} />)
    expect(await axe(container)).toHaveNoViolations()
  })
})
