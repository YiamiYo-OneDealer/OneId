import { renderHook, act, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { HTTPError } from 'ky'
import { useFormMutation } from './useFormMutation'

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

import { toast } from 'sonner'
const mockToastSuccess = vi.mocked(toast.success)
const mockToastError = vi.mocked(toast.error)

function createWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children)
}

function makeHttpError(status: number, body: unknown): HTTPError {
  const response = new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
  return new HTTPError(response, new Request('https://example.com'), {} as never)
}

describe('useFormMutation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('fires durable toast.success on success', async () => {
    const { result } = renderHook(
      () =>
        useFormMutation({
          mutationFn: async () => 'ok',
          messages: { success: 'Role saved', error: 'Failed' },
        }),
      { wrapper: createWrapper() },
    )

    act(() => { result.current.mutate() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(mockToastSuccess).toHaveBeenCalledWith('Role saved', { duration: Infinity })
    expect(mockToastError).not.toHaveBeenCalled()
  })

  it('appends propagation note when propagationNote is true', async () => {
    const { result } = renderHook(
      () =>
        useFormMutation({
          mutationFn: async () => 'ok',
          messages: { success: 'Role saved', error: 'Failed', propagationNote: true },
        }),
      { wrapper: createWrapper() },
    )

    act(() => { result.current.mutate() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(mockToastSuccess).toHaveBeenCalledWith('Role saved', {
      description: 'Changes effective within 5 minutes.',
      duration: Infinity,
    })
  })

  it('overrides message with force-revoke text when forceRevoke is true', async () => {
    const { result } = renderHook(
      () =>
        useFormMutation({
          mutationFn: async () => 'ok',
          messages: {
            success: 'Role saved',
            error: 'Failed',
            forceRevoke: true,
            propagationNote: true,
          },
        }),
      { wrapper: createWrapper() },
    )

    act(() => { result.current.mutate() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(mockToastSuccess).toHaveBeenCalledWith(
      'User must re-authenticate — changes are immediate',
      { duration: Infinity },
    )
  })

  it('fires auto-dismiss toast.error for system errors (5xx)', async () => {
    const httpError = makeHttpError(500, { title: 'Server error' })

    const { result } = renderHook(
      () =>
        useFormMutation({
          mutationFn: async () => { throw httpError },
          messages: { success: 'Saved', error: 'Something went wrong' },
        }),
      { wrapper: createWrapper() },
    )

    act(() => { result.current.mutate() })
    await waitFor(() => expect(result.current.isError).toBe(true))

    expect(mockToastError).toHaveBeenCalledWith('Something went wrong', { duration: 8000 })
    expect(mockToastSuccess).not.toHaveBeenCalled()
  })

  it('calls onValidationError and does NOT fire toast.error for 422 with field errors', async () => {
    const httpError = makeHttpError(422, {
      type: 'https://oneid.onedealer.com/errors/validation',
      title: 'Validation failed',
      status: 422,
      errors: { name: ['Name is required.'] },
    })

    const onValidationError = vi.fn()

    const { result } = renderHook(
      () =>
        useFormMutation({
          mutationFn: async () => { throw httpError },
          messages: { success: 'Saved', error: 'Failed' },
          onValidationError,
        }),
      { wrapper: createWrapper() },
    )

    act(() => { result.current.mutate() })
    await waitFor(() => expect(result.current.isError).toBe(true))

    await waitFor(() => expect(onValidationError).toHaveBeenCalledWith({ name: ['Name is required.'] }))
    expect(mockToastError).not.toHaveBeenCalled()
  })
})
