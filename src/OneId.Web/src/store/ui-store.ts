import { create } from 'zustand'

interface UiState {
  isFormDirty: boolean
  setFormDirty: (dirty: boolean) => void
}

export const useUiStore = create<UiState>((set) => ({
  isFormDirty: false,
  setFormDirty: (dirty) => set({ isFormDirty: dirty }),
}))
