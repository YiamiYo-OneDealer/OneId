import { useState } from 'react'

const STORAGE_KEY = 'oneid:sidebar:collapsed'

export function useSidebarState() {
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === 'true'
    } catch {
      return false
    }
  })

  const toggle = () => {
    setCollapsed((prev) => {
      const next = !prev
      try {
        localStorage.setItem(STORAGE_KEY, String(next))
      } catch {
        // localStorage unavailable in tests / private browsing
      }
      return next
    })
  }

  return { collapsed, toggle }
}
