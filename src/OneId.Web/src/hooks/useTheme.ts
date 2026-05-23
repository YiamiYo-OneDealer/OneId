import { create } from 'zustand'

const STORAGE_KEY = 'oneid:theme'

type Theme = 'dark' | 'light'

function getInitialTheme(): Theme {
  try {
    if (localStorage.getItem(STORAGE_KEY) === 'light') return 'light'
  } catch {}
  return 'dark'
}

function applyTheme(theme: Theme) {
  document.documentElement.classList.toggle('dark', theme === 'dark')
  try {
    localStorage.setItem(STORAGE_KEY, theme)
  } catch {}
}

interface ThemeState {
  theme: Theme
  toggle: () => void
}

export const useTheme = create<ThemeState>((set, get) => ({
  theme: getInitialTheme(),
  toggle: () => {
    const next = get().theme === 'dark' ? 'light' : 'dark'
    applyTheme(next)
    set({ theme: next })
  },
}))
