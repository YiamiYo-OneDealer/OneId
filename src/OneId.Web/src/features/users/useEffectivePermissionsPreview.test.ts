import { vi, it, expect, describe, beforeEach, afterEach } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { useEffectivePermissionsPreview } from './api'
import { apiClient } from '@/lib/api-client'

vi.mock('@/lib/api-client', () => ({
  apiClient: {
    post: vi.fn(),
  },
}))

const mockPost = vi.mocked(apiClient.post)

const emptyResponse = {
  userId: 'user-1',
  resolvedAt: new Date().toISOString(),
  hasGroupAssignments: false,
  permissions: [],
}

describe('useEffectivePermissionsPreview', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
    mockPost.mockReturnValue({
      json: () => Promise.resolve(emptyResponse),
    } as ReturnType<typeof apiClient.post>)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('debounces: 5 rapid payload changes within 200ms result in exactly 1 fetch call', async () => {
    const { rerender } = renderHook(
      ({ payload }) => useEffectivePermissionsPreview('user-1', payload),
      { initialProps: { payload: { groupIds: ['g1'] } } },
    )

    // 5 rapid changes, each 40ms apart (200ms total, within 350ms debounce)
    for (let i = 2; i <= 5; i++) {
      act(() => vi.advanceTimersByTime(40))
      rerender({ payload: { groupIds: [`g${i}`] } })
    }

    // Advance past debounce window
    await act(() => vi.advanceTimersByTimeAsync(400))

    expect(mockPost).toHaveBeenCalledTimes(1)
  })

  it('does not fire when previewPayload is null', async () => {
    renderHook(() => useEffectivePermissionsPreview('user-1', null))

    await act(() => vi.advanceTimersByTimeAsync(500))

    expect(mockPost).not.toHaveBeenCalled()
  })
})
