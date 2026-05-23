import { useMatches, Link } from 'react-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'

interface RouteHandle {
  breadcrumb?: () => string
}

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

function segmentToLabel(segment: string): string {
  return segment
    .split('-')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ')
}

export function Breadcrumbs() {
  const matches = useMatches()

  const crumbs = matches
    .filter((m) => m.pathname !== '/')
    .map((m) => {
      const handle = m.handle as RouteHandle | undefined
      const segments = m.pathname.split('/').filter(Boolean)
      const lastSegment = segments[segments.length - 1] ?? ''
      const label = handle?.breadcrumb?.() ?? segmentToLabel(lastSegment)
      return { label, to: m.pathname, lastSegment }
    })
    .filter((c) => c.label && !UUID_RE.test(c.lastSegment))
    .map(({ label, to }) => ({ label, to }))

  if (crumbs.length === 0) return null

  return (
    <Breadcrumb>
      <BreadcrumbList>
        {crumbs.map((crumb, i) => (
          <span key={crumb.to} className="flex items-center gap-1.5">
            {i > 0 && <BreadcrumbSeparator />}
            <BreadcrumbItem>
              {i === crumbs.length - 1 ? (
                <BreadcrumbPage>{crumb.label}</BreadcrumbPage>
              ) : (
                <BreadcrumbLink asChild>
                  <Link to={crumb.to}>{crumb.label}</Link>
                </BreadcrumbLink>
              )}
            </BreadcrumbItem>
          </span>
        ))}
      </BreadcrumbList>
    </Breadcrumb>
  )
}
