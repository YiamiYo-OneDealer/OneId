import { useState } from 'react'
import { flushSync } from 'react-dom'
import { useNavigate, Link, useBlocker } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useCreateTenant } from '@/queries/hooks'
import { mockStore, mockDelay } from '@/mocks/store'
import { queryKeys } from '@/queries/keys'
import { cn } from '@/lib/utils'
import type { TenantStatus } from '@/mocks/types'

const STEPS = [
  { id: 1 as const, label: 'Tenant Details' },
  { id: 2 as const, label: 'License Configuration' },
  { id: 3 as const, label: 'Initial Tenant Admin' },
  { id: 4 as const, label: 'Review & Confirm' },
] as const

// ── Step 1: Tenant Details ────────────────────────────────────────────────────

function StepTenantDetails({
  tenantName,
  tenantNameError,
  tenantStatus,
  onNameChange,
  onStatusChange,
  onBlurName,
}: {
  tenantName: string
  tenantNameError: string
  tenantStatus: TenantStatus
  onNameChange: (v: string) => void
  onStatusChange: (v: TenantStatus) => void
  onBlurName: () => void
}) {
  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <Label htmlFor="tenant-name">Name *</Label>
        <Input
          id="tenant-name"
          value={tenantName}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) => onNameChange(e.target.value)}
          onBlur={onBlurName}
          placeholder="e.g. Acme Corp"
        />
        {tenantNameError && <p className="text-sm text-destructive">{tenantNameError}</p>}
      </div>
      <div className="space-y-1">
        <Label>Status</Label>
        <div className="flex gap-2">
          <Button
            type="button"
            variant={tenantStatus === 'active' ? 'default' : 'outline'}
            size="sm"
            onClick={() => onStatusChange('active')}
            aria-pressed={tenantStatus === 'active'}
          >
            Active
          </Button>
          <Button
            type="button"
            variant={tenantStatus === 'suspended' ? 'default' : 'outline'}
            size="sm"
            onClick={() => onStatusChange('suspended')}
            aria-pressed={tenantStatus === 'suspended'}
          >
            Suspended
          </Button>
        </div>
      </div>
    </div>
  )
}

// ── Step 2: License Configuration ────────────────────────────────────────────

function StepLicense({
  maxSeats,
  maxSeatsError,
  onMaxSeatsChange,
}: {
  maxSeats: string
  maxSeatsError: string
  onMaxSeatsChange: (v: string) => void
}) {
  const trimmed = maxSeats.trim()
  const seatsNum = Number(trimmed)
  const isValidSeat = trimmed !== '' && Number.isInteger(seatsNum) && seatsNum >= 1
  const preview = isValidSeat
    ? `This tenant will allow up to ${seatsNum} active users.`
    : 'This tenant will have no seat limit.'

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <Label htmlFor="max-seats">Max Seats</Label>
        <Input
          id="max-seats"
          type="number"
          min={1}
          value={maxSeats}
          onChange={(e: React.ChangeEvent<HTMLInputElement>) => onMaxSeatsChange(e.target.value)}
          placeholder="e.g. 50"
        />
        {maxSeatsError && <p className="text-sm text-destructive">{maxSeatsError}</p>}
      </div>
      <p className="text-sm text-muted-foreground">{preview}</p>
    </div>
  )
}

// ── Step 3: Initial Tenant Admin ──────────────────────────────────────────────

function StepInitialAdmin({
  adminName,
  adminNameError,
  adminEmail,
  adminEmailError,
  skipAdmin,
  onNameChange,
  onEmailChange,
  onSkipChange,
  onBlurName,
  onBlurEmail,
}: {
  adminName: string
  adminNameError: string
  adminEmail: string
  adminEmailError: string
  skipAdmin: boolean
  onNameChange: (v: string) => void
  onEmailChange: (v: string) => void
  onSkipChange: (v: boolean) => void
  onBlurName: () => void
  onBlurEmail: () => void
}) {
  return (
    <div className="space-y-4">
      <label className="flex items-center gap-2 cursor-pointer">
        <input
          type="checkbox"
          checked={skipAdmin}
          onChange={(e) => onSkipChange(e.target.checked)}
          className="h-4 w-4"
        />
        <span className="text-sm text-foreground">Skip for now — designate Tenant Admin later</span>
      </label>
      {!skipAdmin && (
        <>
          <div className="space-y-1">
            <Label htmlFor="admin-name">Name *</Label>
            <Input
              id="admin-name"
              value={adminName}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => onNameChange(e.target.value)}
              onBlur={onBlurName}
              placeholder="e.g. Jane Doe"
            />
            {adminNameError && <p className="text-sm text-destructive">{adminNameError}</p>}
          </div>
          <div className="space-y-1">
            <Label htmlFor="admin-email">Email *</Label>
            <Input
              id="admin-email"
              type="email"
              value={adminEmail}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => onEmailChange(e.target.value)}
              onBlur={onBlurEmail}
              placeholder="e.g. jane@example.com"
            />
            {adminEmailError && <p className="text-sm text-destructive">{adminEmailError}</p>}
          </div>
        </>
      )}
    </div>
  )
}

// ── Step 4: Review & Confirm ──────────────────────────────────────────────────

function StepReview({
  tenantName,
  tenantStatus,
  maxSeats,
  adminName,
  adminEmail,
  skipAdmin,
  onEditStep,
}: {
  tenantName: string
  tenantStatus: TenantStatus
  maxSeats: string
  adminName: string
  adminEmail: string
  skipAdmin: boolean
  onEditStep: (step: 1 | 2 | 3) => void
}) {
  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-border p-4 space-y-2">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium text-foreground">Tenant Details</h2>
          <Button variant="ghost" size="sm" onClick={() => onEditStep(1)}>Edit</Button>
        </div>
        <p className="text-sm text-muted-foreground">Name: <span className="text-foreground">{tenantName}</span></p>
        <p className="text-sm text-muted-foreground">Status: <span className="text-foreground capitalize">{tenantStatus}</span></p>
      </section>

      <section className="rounded-lg border border-border p-4 space-y-2">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium text-foreground">License Configuration</h2>
          <Button variant="ghost" size="sm" onClick={() => onEditStep(2)}>Edit</Button>
        </div>
        <p className="text-sm text-muted-foreground">
          Max Seats: <span className="text-foreground">{maxSeats.trim() || 'Unlimited'}</span>
        </p>
      </section>

      <section className="rounded-lg border border-border p-4 space-y-2">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium text-foreground">Initial Tenant Admin</h2>
          <Button variant="ghost" size="sm" onClick={() => onEditStep(3)}>Edit</Button>
        </div>
        {skipAdmin ? (
          <p className="text-sm text-muted-foreground">Will be designated later</p>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">Name: <span className="text-foreground">{adminName}</span></p>
            <p className="text-sm text-muted-foreground">Email: <span className="text-foreground">{adminEmail}</span></p>
          </>
        )}
      </section>
    </div>
  )
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function TenantProvisioningPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const createTenant = useCreateTenant()

  // Navigation
  const [currentStep, setCurrentStep] = useState<1 | 2 | 3 | 4>(1)
  const [isDirty, setIsDirty] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  // Step 1: Tenant Details
  const [tenantName, setTenantName] = useState('')
  const [tenantNameError, setTenantNameError] = useState('')
  const [tenantStatus, setTenantStatus] = useState<TenantStatus>('active')

  // Step 2: License Configuration
  const [maxSeats, setMaxSeats] = useState('')
  const [maxSeatsError, setMaxSeatsError] = useState('')

  // Step 3: Initial Tenant Admin
  const [adminName, setAdminName] = useState('')
  const [adminNameError, setAdminNameError] = useState('')
  const [adminEmail, setAdminEmail] = useState('')
  const [adminEmailError, setAdminEmailError] = useState('')
  const [skipAdmin, setSkipAdmin] = useState(false)

  // unstable_useBlocker — API unstable in RR7 minor versions, does not intercept tab close. Scope: F-3 only.
  const blocker = useBlocker(isDirty)

  const markDirty = () => setIsDirty(true)

  const validateStep1 = () => {
    if (!tenantName.trim()) { setTenantNameError('Tenant name is required.'); return false }
    setTenantNameError('')
    return true
  }

  const validateStep2 = () => {
    const trimmed = maxSeats.trim()
    const num = Number(trimmed)
    if (trimmed && (!Number.isInteger(num) || num < 1)) {
      setMaxSeatsError('Enter a positive number, or leave blank for no seat limit.')
      return false
    }
    setMaxSeatsError('')
    return true
  }

  const validateStep3 = () => {
    if (skipAdmin) return true
    let ok = true
    if (!adminName.trim()) { setAdminNameError('Name is required.'); ok = false }
    else setAdminNameError('')
    if (!adminEmail.trim()) { setAdminEmailError('Email is required.'); ok = false }
    else if (!adminEmail.includes('@')) { setAdminEmailError('Enter a valid email address.'); ok = false }
    else setAdminEmailError('')
    return ok
  }

  const handleNext = () => {
    if (currentStep === 1 && !validateStep1()) return
    if (currentStep === 2 && !validateStep2()) return
    if (currentStep === 3 && !validateStep3()) return
    setCurrentStep((s) => (s < 4 ? ((s + 1) as 1 | 2 | 3 | 4) : s))
  }

  const handleBack = () => {
    setCurrentStep((s) => (s > 1 ? ((s - 1) as 1 | 2 | 3 | 4) : s))
  }

  const handleSubmit = async () => {
    setSubmitError(null)
    try {
      const newTenant = await createTenant.mutateAsync({
        name: tenantName.trim(),
        status: tenantStatus,
        seatUsage: { used: 0, max: maxSeats.trim() ? Number(maxSeats.trim()) : null },
      })
      if (!skipAdmin && adminName.trim() && adminEmail.trim()) {
        await mockDelay(200)
        mockStore.createUser({
          tenantId: newTenant.id,
          name: adminName.trim(),
          email: adminEmail.trim(),
          status: 'active',
          groupIds: [],
          lastLogin: null,
        })
        queryClient.invalidateQueries({ queryKey: queryKeys.users(newTenant.id) })
      }
      flushSync(() => setIsDirty(false))
      navigate(`/internal/tenants/${newTenant.id}`)
    } catch {
      setSubmitError('Failed to create tenant. Please try again.')
    }
  }

  const isSubmitting = createTenant.isPending

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex items-center gap-4">
        <Link to="/internal/tenants" className="text-sm text-muted-foreground hover:text-foreground">
          ← All Tenants
        </Link>
        <h1 className="text-xl font-semibold text-foreground">New Tenant</h1>
      </div>

      <div className="flex gap-8">
        {/* Vertical stepper indicator */}
        <ol className="min-w-[180px] space-y-4">
          {STEPS.map((step) => (
            <li key={step.id} className="flex items-center gap-3">
              <span
                className={cn(
                  'flex h-7 w-7 items-center justify-center rounded-full text-sm font-medium',
                  currentStep === step.id
                    ? 'bg-primary text-background'
                    : currentStep > step.id
                      ? 'bg-primary/20 text-primary'
                      : 'bg-card text-muted-foreground',
                )}
              >
                {step.id}
              </span>
              <span
                className={cn(
                  'text-sm',
                  currentStep === step.id
                    ? 'font-medium text-foreground'
                    : 'text-muted-foreground',
                )}
              >
                {step.label}
              </span>
            </li>
          ))}
        </ol>

        {/* Step content */}
        <div className="flex-1 space-y-6">
          {currentStep === 1 && (
            <StepTenantDetails
              tenantName={tenantName}
              tenantNameError={tenantNameError}
              tenantStatus={tenantStatus}
              onNameChange={(v) => { setTenantName(v); markDirty() }}
              onStatusChange={(v) => { setTenantStatus(v); markDirty() }}
              onBlurName={validateStep1}
            />
          )}
          {currentStep === 2 && (
            <StepLicense
              maxSeats={maxSeats}
              maxSeatsError={maxSeatsError}
              onMaxSeatsChange={(v) => { setMaxSeats(v); markDirty() }}
            />
          )}
          {currentStep === 3 && (
            <StepInitialAdmin
              adminName={adminName}
              adminNameError={adminNameError}
              adminEmail={adminEmail}
              adminEmailError={adminEmailError}
              skipAdmin={skipAdmin}
              onNameChange={(v) => { setAdminName(v); markDirty() }}
              onEmailChange={(v) => { setAdminEmail(v); markDirty() }}
              onSkipChange={setSkipAdmin}
              onBlurName={() => { if (!skipAdmin) { if (!adminName.trim()) setAdminNameError('Name is required.'); else setAdminNameError('') } }}
              onBlurEmail={() => { if (!skipAdmin) { if (!adminEmail.trim()) setAdminEmailError('Email is required.'); else if (!adminEmail.includes('@')) setAdminEmailError('Enter a valid email address.'); else setAdminEmailError('') } }}
            />
          )}
          {currentStep === 4 && (
            <StepReview
              tenantName={tenantName}
              tenantStatus={tenantStatus}
              maxSeats={maxSeats}
              adminName={adminName}
              adminEmail={adminEmail}
              skipAdmin={skipAdmin}
              onEditStep={(step) => setCurrentStep(step)}
            />
          )}

          {submitError && <p className="text-sm text-destructive">{submitError}</p>}

          <div className="flex justify-between">
            {currentStep > 1 ? (
              <Button variant="outline" onClick={handleBack} disabled={isSubmitting}>
                Back
              </Button>
            ) : (
              <div />
            )}
            {currentStep < 4 ? (
              <Button onClick={handleNext}>Next</Button>
            ) : (
              <Button onClick={handleSubmit} disabled={isSubmitting}>
                {isSubmitting ? 'Creating…' : 'Create Tenant'}
              </Button>
            )}
          </div>
        </div>
      </div>

      {/* Unsaved-changes guard — blocker.state driven */}
      <Dialog open={blocker.state === 'blocked'} onOpenChange={(open) => { if (!open) blocker.reset?.() }}>
        <DialogContent className="max-w-sm">
          <DialogHeader>
            <DialogTitle>Discard changes?</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            You have unsaved changes. Leaving will discard this new tenant. Continue?
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => blocker.reset?.()}>Stay</Button>
            <Button variant="destructive" onClick={() => blocker.proceed?.()}>Leave</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
