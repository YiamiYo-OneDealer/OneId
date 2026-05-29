import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip'
import { DimensionalScopeSummary } from '@/components/shared/DimensionalScopeSummary'
import { EffectivePermissionsPanel } from '@/features/users/components/EffectivePermissions'
import { useCreateUser } from '@/queries/hooks'
import { useGroups } from '@/queries/hooks'
import { useTenantStore } from '@/store/tenant-store'
import { apiClient } from '@/lib/api-client'
import { queryKeys } from '@/queries/keys'
import type { PagedResponse, UserDto } from '@/api/types'

type UserStatus = 'active' | 'inactive'

const STEPS = [
  { id: 1 as const, label: 'User Details' },
  { id: 2 as const, label: 'Group Assignments' },
  { id: 3 as const, label: 'Dimension Assignments' },
  { id: 4 as const, label: 'Review & Confirm' },
] as const

// ── Step 1: User Details ──────────────────────────────────────────────────────

function StepUserDetails({
  name,
  nameError,
  email,
  emailError,
  status,
  onNameChange,
  onEmailChange,
  onStatusChange,
  onBlurName,
  onBlurEmail,
}: {
  name: string
  nameError: string
  email: string
  emailError: string
  status: UserStatus
  onNameChange: (v: string) => void
  onEmailChange: (v: string) => void
  onStatusChange: (v: UserStatus) => void
  onBlurName: () => void
  onBlurEmail: () => void
}) {
  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <Label htmlFor="user-name">
          Name <span className="text-destructive">*</span>
        </Label>
        <Input
          id="user-name"
          value={name}
          onChange={(e) => onNameChange(e.target.value)}
          onBlur={onBlurName}
          placeholder="e.g. Jane Smith"
        />
        {nameError && <p className="text-sm text-destructive">{nameError}</p>}
      </div>
      <div className="space-y-1">
        <Label htmlFor="user-email">
          Email <span className="text-destructive">*</span>
        </Label>
        <Input
          id="user-email"
          type="email"
          value={email}
          onChange={(e) => onEmailChange(e.target.value)}
          onBlur={onBlurEmail}
          placeholder="e.g. jane@example.com"
        />
        {emailError && <p className="text-sm text-destructive">{emailError}</p>}
      </div>
      <div className="space-y-1">
        <Label>Status</Label>
        <div className="flex gap-2">
          <Button
            type="button"
            variant={status === 'active' ? 'default' : 'outline'}
            size="sm"
            onClick={() => onStatusChange('active')}
            aria-pressed={status === 'active'}
          >
            Active
          </Button>
          <Button
            type="button"
            variant={status === 'inactive' ? 'default' : 'outline'}
            size="sm"
            onClick={() => onStatusChange('inactive')}
            aria-pressed={status === 'inactive'}
          >
            Inactive
          </Button>
        </div>
      </div>
    </div>
  )
}

// ── Step 2: Group Assignments ─────────────────────────────────────────────────

function StepGroupAssignments({
  tenantId,
  selectedGroupIds,
  onToggleGroup,
}: {
  tenantId: string
  selectedGroupIds: string[]
  onToggleGroup: (groupId: string) => void
}) {
  const [search, setSearch] = useState('')
  const { data: groups = [] } = useGroups(tenantId)
  const filtered = groups.filter((g) => g.name.toLowerCase().includes(search.toLowerCase()))

  return (
    <div className="flex gap-6">
      <div className="w-64 space-y-2">
        <Input
          placeholder="Search groups…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <div className="space-y-1 max-h-64 overflow-y-auto">
          {filtered.map((group) => (
            <div key={group.id} className="flex items-center gap-2 py-1">
              <Checkbox
                id={`group-${group.id}`}
                checked={selectedGroupIds.includes(group.id)}
                onCheckedChange={() => onToggleGroup(group.id)}
              />
              <label htmlFor={`group-${group.id}`} className="text-sm cursor-pointer">
                {group.name}
              </label>
            </div>
          ))}
          {filtered.length === 0 && (
            <p className="text-sm text-muted-foreground py-2">No groups found.</p>
          )}
        </div>
      </div>
      <div className="flex-1">
        <p className="text-sm font-medium text-foreground mb-2">Permission Preview</p>
        <EffectivePermissionsPanel
          mode="preview"
          userId="__preview__"
          previewPayload={{ groupIds: selectedGroupIds }}
        />
      </div>
    </div>
  )
}

// ── Step 3: Dimension Assignments ─────────────────────────────────────────────

function StepDimensionAssignments() {
  return (
    <div className="space-y-3">
      <DimensionalScopeSummary roleName="New User" restrictions={{}} />
      <p className="text-sm text-muted-foreground">
        Dimension assignments can be configured after user creation.
      </p>
    </div>
  )
}

// ── Step 4: Review & Confirm ──────────────────────────────────────────────────

function StepReview({
  name,
  email,
  status,
  selectedGroupIds,
  groupNames,
  onEditStep,
  atSeatLimit,
  isSubmitting,
  onSubmit,
}: {
  name: string
  email: string
  status: UserStatus
  selectedGroupIds: string[]
  groupNames: string[]
  onEditStep: (step: 1 | 2 | 3) => void
  atSeatLimit: boolean
  isSubmitting: boolean
  onSubmit: () => void
}) {
  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-foreground">User Details</h3>
          <Button variant="ghost" size="sm" onClick={() => onEditStep(1)}>
            Edit
          </Button>
        </div>
        <div className="text-sm text-muted-foreground space-y-1 pl-2">
          <p>Name: <span className="text-foreground">{name}</span></p>
          <p>Email: <span className="text-foreground">{email}</span></p>
          <p>Status: <span className="text-foreground capitalize">{status}</span></p>
        </div>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-foreground">Group Assignments</h3>
          <Button variant="ghost" size="sm" onClick={() => onEditStep(2)}>
            Edit
          </Button>
        </div>
        <div className="text-sm text-muted-foreground pl-2">
          {groupNames.length === 0 ? (
            <p>No groups assigned</p>
          ) : (
            <p>{groupNames.join(', ')}</p>
          )}
        </div>
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold text-foreground">Dimension Assignments</h3>
          <Button variant="ghost" size="sm" onClick={() => onEditStep(3)}>
            Edit
          </Button>
        </div>
        <p className="text-sm text-muted-foreground pl-2">Configured after user creation.</p>
      </div>

      <div className="space-y-2">
        <h3 className="text-sm font-semibold text-foreground">Effective Permissions Preview</h3>
        <EffectivePermissionsPanel
          mode="preview"
          userId="__preview__"
          previewPayload={{ groupIds: selectedGroupIds }}
        />
      </div>

      <div className="pt-2">
        {atSeatLimit ? (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <span tabIndex={0}>
                  <Button disabled>Create User</Button>
                </span>
              </TooltipTrigger>
              <TooltipContent>Seat limit reached</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        ) : (
          <Button onClick={onSubmit} disabled={isSubmitting}>
            {isSubmitting ? 'Creating…' : 'Create User'}
          </Button>
        )}
      </div>
    </div>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export function NewUserPage() {
  const navigate = useNavigate()
  const activeTenantId = useTenantStore((s) => s.activeTenantId)
  const tenantId = activeTenantId ?? ''
  const { data: groups = [] } = useGroups(tenantId)
  const createUser = useCreateUser(tenantId)

  const [currentStep, setCurrentStep] = useState<1 | 2 | 3 | 4>(1)
  const [name, setName] = useState('')
  const [nameError, setNameError] = useState('')
  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState('')
  const [status, setStatus] = useState<UserStatus>('active')
  const [selectedGroupIds, setSelectedGroupIds] = useState<string[]>([])

  // Seat limit: totalCount from users endpoint vs maxSeats (null until Phase 6 licensing is built)
  const { data: seatUsage } = useQuery({
    queryKey: queryKeys.seatUsage(tenantId),
    queryFn: () =>
      apiClient
        .get('api/tenant/users', { searchParams: { pageSize: 1 } })
        .json<PagedResponse<UserDto>>()
        .then((r) => ({ used: r.totalCount, max: null as number | null })),
    enabled: !!tenantId,
  })
  const atSeatLimit = seatUsage !== undefined && seatUsage.max !== null && seatUsage.used >= seatUsage.max

  const validateStep1 = (): boolean => {
    let valid = true
    if (!name.trim()) {
      setNameError('Name is required.')
      valid = false
    } else {
      setNameError('')
    }
    if (!email.trim()) {
      setEmailError('Email is required.')
      valid = false
    } else if (!email.includes('@')) {
      setEmailError('Enter a valid email address.')
      valid = false
    } else {
      setEmailError('')
    }
    return valid
  }

  const handleNext = () => {
    if (currentStep === 1 && !validateStep1()) return
    setCurrentStep((s) => (s < 4 ? ((s + 1) as 1 | 2 | 3 | 4) : s))
  }

  const handleBack = () => {
    setCurrentStep((s) => (s > 1 ? ((s - 1) as 1 | 2 | 3 | 4) : s))
  }

  const handleToggleGroup = (groupId: string) => {
    setSelectedGroupIds((prev) =>
      prev.includes(groupId) ? prev.filter((id) => id !== groupId) : [...prev, groupId],
    )
  }

  const handleSubmit = async () => {
    try {
      const newUser = await createUser.mutateAsync({ email: email.trim(), displayName: name.trim() || null })
      if (selectedGroupIds.length > 0) {
        await Promise.all(
          selectedGroupIds.map(groupId =>
            apiClient.put(`api/tenant/groups/${groupId}/members`, { json: { userId: newUser.id } })
          )
        )
      }
      toast.success('User created.')
      navigate('/tenant/users')
    } catch {
      toast.error('Failed to create user.')
    }
  }

  const groupNames = groups
    .filter((g) => selectedGroupIds.includes(g.id))
    .map((g) => g.name)

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">New User</h1>
      </div>

      <div className="flex gap-8">
        {/* Vertical stepper sidebar */}
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
                  currentStep === step.id ? 'font-medium text-foreground' : 'text-muted-foreground',
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
            <StepUserDetails
              name={name}
              nameError={nameError}
              email={email}
              emailError={emailError}
              status={status}
              onNameChange={setName}
              onEmailChange={setEmail}
              onStatusChange={setStatus}
              onBlurName={() => {
                if (!name.trim()) setNameError('Name is required.')
                else setNameError('')
              }}
              onBlurEmail={() => {
                if (!email.trim()) setEmailError('Email is required.')
                else if (!email.includes('@')) setEmailError('Enter a valid email address.')
                else setEmailError('')
              }}
            />
          )}
          {currentStep === 2 && (
            <StepGroupAssignments
              tenantId={tenantId}
              selectedGroupIds={selectedGroupIds}
              onToggleGroup={handleToggleGroup}
            />
          )}
          {currentStep === 3 && <StepDimensionAssignments />}
          {currentStep === 4 && (
            <StepReview
              name={name}
              email={email}
              status={status}
              selectedGroupIds={selectedGroupIds}
              groupNames={groupNames}
              onEditStep={(step) => setCurrentStep(step)}
              atSeatLimit={atSeatLimit}
              isSubmitting={createUser.isPending}
              onSubmit={handleSubmit}
            />
          )}

          {currentStep < 4 && (
            <div className="flex justify-between">
              {currentStep > 1 ? (
                <Button variant="outline" onClick={handleBack}>
                  Back
                </Button>
              ) : (
                <div />
              )}
              <Button onClick={handleNext}>Next</Button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
