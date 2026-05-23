export function StubPage({ title }: { title: string }) {
  return (
    <div className="p-8 text-foreground">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="text-muted-foreground mt-2">Implementation — Epic 5c</p>
    </div>
  )
}
