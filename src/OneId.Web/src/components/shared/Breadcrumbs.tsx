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
      const label = handle?.breadcrumb?.() ?? segmentToLabel(segments[segments.length - 1] ?? '')
      return { label, to: m.pathname }
    })
    .filter((c) => c.label && !c.label.match(/^[0-9a-f-]{36}$/))

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
