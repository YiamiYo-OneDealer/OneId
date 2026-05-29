import { useState } from 'react'
import { useParams, useSearchParams } from 'react-router'
import { X } from 'lucide-react'
import { EffectivePermissionsPanel } from '@/features/users/components/EffectivePermissions'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { EmptyState } from '@/components/shared/EmptyState'
import {
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandItem,
} from '@/components/ui/command'
import { useUser } from '@/queries/hooks'
import { useGroups } from '@/queries/hooks'
import { useUserGroups, useAddGroupMember, useRemoveGroupMember } from '@/queries/hooks'
import { useUserDimensions, useSetUserDimensions, useDimensionValues } from '@/queries/hooks'
import { useTenantStore } from '@/store/tenant-store'
import type { GroupDto, UserDimensionValueDto, DimensionValueDto } from '@/api/types'

const DIMENSION_AXES = ['Company', 'Location', 'Branch', 'Make', 'MarketSegment'] as const
type DimensionAxis = typeof DIMENSION_AXES[number]

// ── Groups Tab ────────────────────────────────────────────────────────────────

function GroupsTab({ tenantId, userId }: { tenantId: string; userId: string }) {
  const [pickerOpen, setPickerOpen] = useState(false)
  const [search, setSearch] = useState('')

  const { data: userGroups, isLoading } = useUserGroups(tenantId, userId)
  const { data: allGroups = [] } = useGroups(tenantId)
  const addMember = useAddGroupMember(tenantId)
  const removeMember = useRemoveGroupMember(tenantId)

  const memberGroupIds = new Set((userGroups ?? []).map((g) => g.id))
  const addableGroups = allGroups.filter((g) => !memberGroupIds.has(g.id))
  const filteredAddable = addableGroups.filter((g) =>
    g.name.toLowerCase().includes(search.toLowerCase()),
  )

  const handleRemove = (groupId: string) => {
    removeMember.mutate({ groupId, userId })
  }

  const handleAdd = (group: GroupDto) => {
    addMember.mutate({ groupId: group.id, userId })
    setPickerOpen(false)
    setSearch('')
  }

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">Groups this user belongs to</p>
        <Button
          size="sm"
          variant="outline"
          onClick={() => setPickerOpen(true)}
          disabled={addableGroups.length === 0}
        >
          Add to group
        </Button>
      </div>

      {!userGroups || userGroups.length === 0 ? (
        <EmptyState
          variant="empty"
          title="No group memberships"
          description="This user doesn't belong to any groups yet."
          action={{ label: 'Add to group', onClick: () => setPickerOpen(true) }}
        />
      ) : (
        <div className="space-y-1">
          {userGroups.map((group) => (
            <div
              key={group.id}
              className="flex items-center justify-between rounded-md border border-border px-3 py-2"
            >
              <span className="text-sm">{group.name}</span>
              <Button
                size="sm"
                variant="ghost"
                className="text-destructive hover:text-destructive"
                onClick={() => handleRemove(group.id)}
                disabled={removeMember.isPending}
              >
                Remove
              </Button>
            </div>
          ))}
        </div>
      )}

      <CommandDialog
        open={pickerOpen}
        onOpenChange={setPickerOpen}
        title="Add to group"
        description="Select a group to add this user to"
      >
        <CommandInput
          placeholder="Search groups…"
          value={search}
          onValueChange={setSearch}
        />
        <CommandList>
          <CommandEmpty>No groups available.</CommandEmpty>
          {filteredAddable.map((group) => (
            <CommandItem key={group.id} onSelect={() => handleAdd(group)}>
              {group.name}
            </CommandItem>
          ))}
        </CommandList>
      </CommandDialog>
    </div>
  )
}

// ── Dimensions Tab ────────────────────────────────────────────────────────────

function AxisRow({
  axis,
  assigned,
  available,
  onAdd,
  onRemove,
  isPending,
}: {
  axis: DimensionAxis
  assigned: UserDimensionValueDto[]
  available: DimensionValueDto[]
  onAdd: (valueId: string) => void
  onRemove: (valueId: string) => void
  isPending: boolean
}) {
  const [pickerOpen, setPickerOpen] = useState(false)
  const [search, setSearch] = useState('')

  const assignedIds = new Set(assigned.map((v) => v.id))
  const addableValues = available.filter((v) => !assignedIds.has(v.id))
  const filteredAddable = addableValues.filter((v) =>
    v.value.toLowerCase().includes(search.toLowerCase()),
  )

  const handleAdd = (valueId: string) => {
    onAdd(valueId)
    setPickerOpen(false)
    setSearch('')
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium">{axis}</span>
        <Button
          size="sm"
          variant="ghost"
          onClick={() => setPickerOpen(true)}
          disabled={addableValues.length === 0 || isPending}
        >
          + Add value
        </Button>
      </div>

      <div className="flex flex-wrap gap-1 min-h-[28px]">
        {assigned.length === 0 ? (
          <span className="text-xs text-muted-foreground">No values assigned</span>
        ) : (
          assigned.map((v) => (
            <Badge
              key={v.id}
              variant="secondary"
              className="flex items-center gap-1 pr-1"
            >
              {v.value}
              <button
                type="button"
                onClick={() => onRemove(v.id)}
                disabled={isPending}
                className="ml-1 rounded-full p-0.5 hover:bg-muted focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                aria-label={`Remove ${v.value}`}
              >
                <X size={10} />
              </button>
            </Badge>
          ))
        )}
      </div>

      <CommandDialog
        open={pickerOpen}
        onOpenChange={setPickerOpen}
        title={`Add ${axis} value`}
        description={`Select a ${axis} value to assign`}
      >
        <CommandInput
          placeholder={`Search ${axis} values…`}
          value={search}
          onValueChange={setSearch}
        />
        <CommandList>
          <CommandEmpty>No values available.</CommandEmpty>
          {filteredAddable.map((v) => (
            <CommandItem key={v.id} onSelect={() => handleAdd(v.id)}>
              {v.value}
            </CommandItem>
          ))}
        </CommandList>
      </CommandDialog>
    </div>
  )
}

function DimensionsTab({ tenantId, userId }: { tenantId: string; userId: string }) {
  const { data: dims, isLoading: dimsLoading } = useUserDimensions(tenantId, userId)
  const { data: dimValues, isLoading: valuesLoading } = useDimensionValues(tenantId)
  const setDimensions = useSetUserDimensions(tenantId, userId)

  if (dimsLoading || valuesLoading) {
    return (
      <div className="space-y-4">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    )
  }

  const allCurrentIds = dims
    ? DIMENSION_AXES.flatMap((axis) => (dims[axis] ?? []).map((v) => v.id))
    : []

  const handleAdd = (valueId: string) => {
    setDimensions.mutate({ valueIds: [...allCurrentIds, valueId] })
  }

  const handleRemove = (valueId: string) => {
    setDimensions.mutate({ valueIds: allCurrentIds.filter((id) => id !== valueId) })
  }

  return (
    <div className="space-y-6">
      {DIMENSION_AXES.map((axis) => (
        <AxisRow
          key={axis}
          axis={axis}
          assigned={dims?.[axis] ?? []}
          available={dimValues?.[axis] ?? []}
          onAdd={handleAdd}
          onRemove={handleRemove}
          isPending={setDimensions.isPending}
        />
      ))}
    </div>
  )
}

// ── Main Page ─────────────────────────────────────────────────────────────────

type TabValue = 'permissions' | 'groups' | 'dimensions'

export function UserPermissionsPage() {
  const { userId } = useParams<{ userId: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTenantId = useTenantStore((s) => s.activeTenantId)
  const tenantId = activeTenantId ?? ''

  const tab = (searchParams.get('tab') ?? 'permissions') as TabValue
  const { data: user } = useUser(tenantId, userId ?? '')

  if (!userId) return null

  const handleTabChange = (value: string) => {
    if (value === 'permissions') {
      setSearchParams({})
    } else {
      setSearchParams({ tab: value })
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-foreground">
          {user?.displayName ?? user?.email ?? 'User'}
        </h1>
        {user?.displayName && (
          <p className="text-sm text-muted-foreground">{user.email}</p>
        )}
      </div>

      <Tabs value={tab} onValueChange={handleTabChange}>
        <TabsList>
          <TabsTrigger value="permissions">Permissions</TabsTrigger>
          <TabsTrigger value="groups">Groups</TabsTrigger>
          <TabsTrigger value="dimensions">Dimensions</TabsTrigger>
        </TabsList>

        <TabsContent value="permissions">
          <EffectivePermissionsPanel mode="live" userId={userId} />
        </TabsContent>

        <TabsContent value="groups">
          <GroupsTab tenantId={tenantId} userId={userId} />
        </TabsContent>

        <TabsContent value="dimensions">
          <DimensionsTab tenantId={tenantId} userId={userId} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
