import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet'
import type { AuditLogDto } from '@/api/types'
import { formatTimestamp } from '@/routes/_audit-log-columns'

export function AuditEventSheet({
  entry,
  onClose,
}: {
  entry: AuditLogDto | null
  onClose: () => void
}) {
  const parsedPayload = (() => {
    if (!entry?.payload) return null
    try { return JSON.parse(entry.payload) } catch { return entry.payload }
  })()

  return (
    <Sheet open={!!entry} onOpenChange={(open) => { if (!open) onClose() }}>
      <SheetContent side="right" className="w-[480px] overflow-y-auto">
        <SheetHeader>
          <SheetTitle>Audit Event</SheetTitle>
          <SheetDescription>Read-only event details</SheetDescription>
        </SheetHeader>
        {entry && (
          <div className="mt-4 space-y-4 text-sm">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2">
              <dt className="text-muted-foreground font-medium">Timestamp</dt>
              <dd className="font-mono text-foreground">{formatTimestamp(entry.timestamp)}</dd>

              <dt className="text-muted-foreground font-medium">Actor</dt>
              <dd className="text-foreground">
                {entry.actorUserId ? (
                  <span className="font-mono text-xs">{entry.actorUserId}</span>
                ) : (
                  <span className="italic text-muted-foreground">System</span>
                )}
              </dd>

              <dt className="text-muted-foreground font-medium">Action</dt>
              <dd className="font-mono text-foreground">{entry.action}</dd>

              <dt className="text-muted-foreground font-medium">Entity Type</dt>
              <dd className="text-foreground">{entry.entityType}</dd>

              <dt className="text-muted-foreground font-medium">Entity ID</dt>
              <dd className="font-mono text-xs text-foreground">{entry.entityId}</dd>
            </dl>

            {parsedPayload && (
              <div>
                <p className="text-muted-foreground font-medium mb-1">Payload</p>
                <pre className="rounded-md border border-border bg-card p-3 text-xs text-foreground overflow-x-auto">
                  {typeof parsedPayload === 'string'
                    ? parsedPayload
                    : JSON.stringify(parsedPayload, null, 2)}
                </pre>
              </div>
            )}
          </div>
        )}
      </SheetContent>
    </Sheet>
  )
}
