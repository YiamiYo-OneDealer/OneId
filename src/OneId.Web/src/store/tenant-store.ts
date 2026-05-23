import { create } from 'zustand'

interface TenantState {
  activeTenantId: string | null
  setActiveTenantId: (tenantId: string | null) => void
  clearTenant: () => void
}

export const useTenantStore = create<TenantState>((set) => ({
  activeTenantId: null,
  setActiveTenantId: (tenantId) => set({ activeTenantId: tenantId }),
  clearTenant: () => set({ activeTenantId: null }),
}))
