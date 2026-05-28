import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, describe, it } from 'vitest'
import { MemoryRouter } from 'react-router'
import { ProvenanceChain } from './ProvenanceChain'
import type { ProvenanceNode } from '@/features/users/schemas'

const SHORT_CHAIN: ProvenanceNode[] = [
  { nodeType: 'user', id: 'u-1', label: 'Alice', href: '' },
  { nodeType: 'group', id: 'g-1', label: 'HR Team', href: '/tenant/groups/g-1' },
  { nodeType: 'role', id: 'r-1', label: 'User Manager', href: '/tenant/roles/r-1' },
  { nodeType: 'permission', id: 'od.users.read', label: 'od.users.read', href: '' },
]

const LONG_CHAIN: ProvenanceNode[] = [
  { nodeType: 'user', id: 'u-1', label: 'Alice', href: '' },
  { nodeType: 'group', id: 'g-1', label: 'IT Staff', href: '/tenant/groups/g-1' },
  { nodeType: 'roleSet', id: 'rs-1', label: 'Managers Bundle', href: '/tenant/role-sets/rs-1' },
  { nodeType: 'role', id: 'r-1', label: 'User Manager', href: '/tenant/roles/r-1' },
  { nodeType: 'permission', id: 'od.users.write', label: 'od.users.write', href: '' },
]

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <MemoryRouter>{children}</MemoryRouter>
)

describe('ProvenanceChain', () => {
  describe('collapsed mode', () => {
    it('shows only the group source label', () => {
      render(<ProvenanceChain chain={SHORT_CHAIN} collapsed />, { wrapper })
      expect(screen.getByText('HR Team')).toBeInTheDocument()
      expect(screen.queryByText('Alice')).not.toBeInTheDocument()
      expect(screen.queryByText('User Manager')).not.toBeInTheDocument()
    })

    it('renders group as a link when href is provided', () => {
      render(<ProvenanceChain chain={SHORT_CHAIN} collapsed />, { wrapper })
      const link = screen.getByRole('link', { name: 'HR Team' })
      expect(link).toHaveAttribute('href', '/tenant/groups/g-1')
    })
  })

  describe('expanded mode (short chain)', () => {
    it('renders all nodes', () => {
      render(<ProvenanceChain chain={SHORT_CHAIN} />, { wrapper })
      expect(screen.getByText('Alice')).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'HR Team' })).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'User Manager' })).toBeInTheDocument()
      expect(screen.getByText('od.users.read')).toBeInTheDocument()
    })

    it('does not render Show full chain button for short chains', () => {
      render(<ProvenanceChain chain={SHORT_CHAIN} />, { wrapper })
      expect(screen.queryByText(/show full chain/i)).not.toBeInTheDocument()
    })
  })

  describe('expanded mode (5+ node chain)', () => {
    it('shows "Show full chain ↓" button for long chains', () => {
      render(<ProvenanceChain chain={LONG_CHAIN} />, { wrapper })
      expect(screen.getByRole('button', { name: /show full chain/i })).toBeInTheDocument()
    })

    it('shows all nodes after clicking Show full chain', async () => {
      const user = userEvent.setup()
      render(<ProvenanceChain chain={LONG_CHAIN} />, { wrapper })
      await user.click(screen.getByRole('button', { name: /show full chain/i }))
      expect(screen.getByText('Alice')).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'IT Staff' })).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Managers Bundle' })).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'User Manager' })).toBeInTheDocument()
      expect(screen.getByText('od.users.write')).toBeInTheDocument()
    })

    it('hides Show full chain button after expansion', async () => {
      const user = userEvent.setup()
      render(<ProvenanceChain chain={LONG_CHAIN} />, { wrapper })
      await user.click(screen.getByRole('button', { name: /show full chain/i }))
      expect(screen.queryByRole('button', { name: /show full chain/i })).not.toBeInTheDocument()
    })
  })

  describe('link hrefs', () => {
    it('permission node has no link (no href)', () => {
      render(<ProvenanceChain chain={SHORT_CHAIN} />, { wrapper })
      // Permission label is plain text, not a link
      const permText = screen.getByText('od.users.read')
      expect(permText.tagName).toBe('SPAN')
    })
  })
})
