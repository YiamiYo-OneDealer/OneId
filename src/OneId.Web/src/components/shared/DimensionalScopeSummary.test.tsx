import { render, screen } from '@testing-library/react'
import { expect, describe, it } from 'vitest'
import { axe } from 'vitest-axe'
import { DimensionalScopeSummary } from './DimensionalScopeSummary'

describe('DimensionalScopeSummary', () => {
  it('renders basic sentence with two axes (AC2)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Sales Manager"
        restrictions={{
          Location: ['Amsterdam', 'Utrecht'],
          Make: ['BMW', 'Audi'],
        }}
      />,
    )
    const el = screen.getByText(/Sales Manager — restricted to/)
    expect(el).toBeInTheDocument()
    expect(el.textContent).toContain('Locations: Amsterdam, Utrecht')
    expect(el.textContent).toContain('Makes: BMW, Audi')
    expect(el.textContent).toContain(' and ')
  })

  it('renders all-values shorthand when sentinel "*" is passed (AC3)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Inventory Viewer"
        restrictions={{ Make: ['*'] }}
      />,
    )
    const el = screen.getByText(/Inventory Viewer — restricted to/)
    expect(el.textContent).toContain('Makes: all makes')
    expect(el.textContent).not.toContain('*')
  })

  it('truncates at >3 values and shows +N more button (AC4)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Regional Manager"
        restrictions={{
          Location: ['Amsterdam', 'Utrecht', 'Rotterdam', 'Eindhoven', 'Haarlem'],
        }}
      />,
    )
    expect(screen.getByText(/Amsterdam, Utrecht, Rotterdam/)).toBeInTheDocument()
    const moreBtn = screen.getByRole('button', { name: 'Show all Locations values' })
    expect(moreBtn).toBeInTheDocument()
    expect(moreBtn.textContent).toBe('+2 more')
  })

  it('+N more count is correct for 5 values (AC4)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Regional Manager"
        restrictions={{
          Location: ['Amsterdam', 'Utrecht', 'Rotterdam', 'Eindhoven', 'Haarlem'],
        }}
      />,
    )
    // 5 values → show first 3 + "+2 more"
    const moreBtn = screen.getByRole('button', { name: 'Show all Locations values' })
    expect(moreBtn.textContent?.trim()).toBe('+2 more')
  })

  it('uses singular axis label for a single value (AC5)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Account Manager"
        restrictions={{ Location: ['Amsterdam'] }}
      />,
    )
    const el = screen.getByText(/Account Manager — restricted to/)
    expect(el.textContent).toContain('Location: Amsterdam')
    expect(el.textContent).not.toContain('Locations:')
  })

  it('uses plural axis label for multiple values (AC5)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Account Manager"
        restrictions={{ Location: ['Amsterdam', 'Utrecht'] }}
      />,
    )
    const el = screen.getByText(/Account Manager — restricted to/)
    expect(el.textContent).toContain('Locations: Amsterdam, Utrecht')
  })

  it('renders "Market Segment" label (not "MarketSegment") for MarketSegment axis (AC5)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Segment Lead"
        restrictions={{ MarketSegment: ['Fleet', 'Private'] }}
      />,
    )
    const el = screen.getByText(/Segment Lead — restricted to/)
    expect(el.textContent).toContain('Market Segments:')
  })

  it('renders no-restrictions message when restrictions is empty (AC6)', () => {
    render(
      <DimensionalScopeSummary roleName="Admin" restrictions={{}} />,
    )
    expect(
      screen.getByText(/Admin — no dimensional restrictions \(full scope\)/),
    ).toBeInTheDocument()
  })

  it('renders no-restrictions message when all axes are empty arrays (AC6)', () => {
    render(
      <DimensionalScopeSummary
        roleName="Admin"
        restrictions={{ Location: [], Make: [] }}
      />,
    )
    expect(
      screen.getByText(/Admin — no dimensional restrictions \(full scope\)/),
    ).toBeInTheDocument()
  })

  it('re-renders live when restrictions change (AC7)', () => {
    const { rerender } = render(
      <DimensionalScopeSummary
        roleName="Sales Manager"
        restrictions={{ Location: ['Amsterdam'] }}
      />,
    )
    expect(screen.getByText(/Location: Amsterdam/)).toBeInTheDocument()

    rerender(
      <DimensionalScopeSummary
        roleName="Sales Manager"
        restrictions={{ Make: ['BMW'] }}
      />,
    )
    expect(screen.getByText(/Make: BMW/)).toBeInTheDocument()
    expect(screen.queryByText(/Location:/)).toBeNull()
  })

  it('axe: no accessibility violations (AC8)', async () => {
    const { container } = render(
      <DimensionalScopeSummary
        roleName="Sales Manager"
        restrictions={{
          Location: ['Amsterdam', 'Utrecht'],
          Make: ['BMW', 'Audi', 'Mercedes', 'Volvo'],
        }}
      />,
    )
    expect(await axe(container)).toHaveNoViolations()
  })
})
